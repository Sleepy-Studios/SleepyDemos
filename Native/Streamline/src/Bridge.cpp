#include <windows.h>
#include <d3d12.h>
#include <dxgi1_6.h>
#include <vulkan/vulkan.h>
#include <filesystem>
#include <mutex>
#include <string>
#include <vector>
#include <sstream>
#include <cstring>
#include <atomic>
#include <unordered_map>
#include <set>
#include <array>
#include "IUnityInterface.h"
#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D12.h"
#include "IUnityGraphicsVulkan.h"
#include "sl.h"
#include "sl_security.h"
#include "sl_dlss.h"
#include "DlssFrame.h"

// Editor validation owns explicit input/output resources and ends each validation
// session after a GPU fence. Production presentation is a separate integration gate.
namespace
{
    IUnityInterfaces* interfaces = nullptr;
    IUnityGraphics* graphics = nullptr;
    HMODULE interposer = nullptr;
    std::recursive_mutex stateMutex;
    std::filesystem::path libraryDirectory;
    std::string report = R"({"schemaVersion":1,"state":"NotProbed","features":[]})";
    std::string lastLog;
    std::mutex logMutex;
    std::string lastSdkError;
    bool initialized = false;
    bool sdkFaulted = false;
    bool vulkanInterceptRegistered = false;
    bool vulkanInitializationObserved = false;
    bool vulkanInterposerActive = false;
    std::atomic<uint32_t> vulkanPresentCount{0};
    PFN_vkGetInstanceProcAddr sdkGetInstanceProcAddr = nullptr;
    PFN_vkGetDeviceProcAddr sdkGetDeviceProcAddr = nullptr;
    PFN_vkQueuePresentKHR sdkQueuePresent = nullptr;
    sl::Result deviceBindingResult = sl::Result::eErrorDeviceNotCreated;
    int eventId = 0;
    PFun_slInit* initialize = nullptr;
    PFun_slShutdown* shutdown = nullptr;
    PFun_slGetFeatureRequirements* requirements = nullptr;
    PFun_slIsFeatureSupported* supported = nullptr;
    PFun_slSetD3DDevice* setDevice = nullptr;
    PFun_slGetFeatureFunction* getFunction = nullptr;
    PFun_slGetNewFrameToken* newFrameToken = nullptr;
    PFun_slSetConstants* setConstants = nullptr;
    PFun_slSetTagForFrame* setTags = nullptr;
    PFun_slEvaluateFeature* evaluate = nullptr;
    PFun_slFreeResources* freeResources = nullptr;
    PFun_slDLSSSetOptions* setDlssOptions = nullptr;
    std::unordered_map<uintptr_t, DlssFrame> frameRequests;
    std::set<uint32_t> activeViewports;
    struct ViewportConfiguration { uint32_t mode, width, height; };
    std::unordered_map<uint32_t, ViewportConfiguration> viewportConfigurations;
    struct VulkanView
    {
        VkDevice device = VK_NULL_HANDLE;
        VkImage image = VK_NULL_HANDLE;
        VkImageView view = VK_NULL_HANDLE;
        PFN_vkDestroyImageView destroy = nullptr;
    };
    std::unordered_map<uint32_t, std::array<VulkanView, 4>> viewportViews;

    // Only call after host GPU completion and successful SDK viewport release.
    void ReleaseVulkanViews(uint32_t viewport)
    {
        auto found = viewportViews.find(viewport);
        if (found == viewportViews.end()) return;
        for (auto& entry : found->second)
            if (entry.view) entry.destroy(entry.device, entry.view, nullptr);
        viewportViews.erase(found);
    }

    sl::Result PrepareVulkanResource(IUnityGraphicsVulkanV2* api, void* texture, bool writable,
        uint32_t viewport, size_t slot, sl::Resource& resource)
    {
        UnityVulkanImage image{};
        const auto layout = writable ? VK_IMAGE_LAYOUT_GENERAL : VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
        const VkAccessFlags access = writable ? VK_ACCESS_SHADER_READ_BIT | VK_ACCESS_SHADER_WRITE_BIT : VK_ACCESS_SHADER_READ_BIT;
        if (!api->AccessTexture(texture, UnityVulkanWholeImage, layout, VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT,
            access, kUnityVulkanResourceAccess_PipelineBarrier, &image)) return sl::Result::eErrorInvalidIntegration;
        if (!image.image || image.type != VK_IMAGE_TYPE_2D || image.samples != VK_SAMPLE_COUNT_1_BIT ||
            image.layers != 1 || image.mipCount != 1 || image.aspect != VK_IMAGE_ASPECT_COLOR_BIT ||
            !(image.usage & (writable ? VK_IMAGE_USAGE_STORAGE_BIT : VK_IMAGE_USAGE_SAMPLED_BIT)))
            return sl::Result::eErrorInvalidParameter;
        auto instance = api->Instance();
        auto& cached = viewportViews[viewport][slot];
        // Host textures must remain stable until explicit viewport release, including after resize.
        if (cached.view && (cached.image != image.image || cached.device != instance.device))
            return sl::Result::eErrorInvalidIntegration;
        if (!cached.view)
        {
            if (!instance.device || !instance.getInstanceProcAddr) return sl::Result::eErrorDeviceNotCreated;
            auto getDeviceProc = reinterpret_cast<PFN_vkGetDeviceProcAddr>(instance.getInstanceProcAddr(instance.instance, "vkGetDeviceProcAddr"));
            if (!getDeviceProc) return sl::Result::eErrorMissingOrInvalidAPI;
            auto create = reinterpret_cast<PFN_vkCreateImageView>(getDeviceProc(instance.device, "vkCreateImageView"));
            auto destroy = reinterpret_cast<PFN_vkDestroyImageView>(getDeviceProc(instance.device, "vkDestroyImageView"));
            if (!create || !destroy) return sl::Result::eErrorMissingOrInvalidAPI;
            VkImageViewCreateInfo info{};
            info.sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
            info.image = image.image;
            info.viewType = VK_IMAGE_VIEW_TYPE_2D;
            info.format = image.format;
            info.subresourceRange = {VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1};
            VkImageView view = VK_NULL_HANDLE;
            if (create(instance.device, &info, nullptr, &view) != VK_SUCCESS) return sl::Result::eErrorInvalidIntegration;
            cached = {instance.device, image.image, view, destroy};
        }
        resource = sl::Resource(sl::ResourceType::eTex2d, reinterpret_cast<void*>(image.image),
            reinterpret_cast<void*>(image.memory.memory), reinterpret_cast<void*>(cached.view), static_cast<uint32_t>(image.layout));
        resource.width = image.extent.width;
        resource.height = image.extent.height;
        resource.nativeFormat = static_cast<uint32_t>(image.format);
        resource.mipLevels = 1;
        resource.arrayLayers = 1;
        resource.flags = 0;
        resource.usage = image.usage;
        return sl::Result::eOk;
    }
    uintptr_t nextRequest = 0;
    std::string frameReport = R"({"requestId":0,"state":"NotEvaluated"})";
    std::string resourceReport = R"({"state":"NotProbed"})";

    std::string Quote(const std::string& value)
    {
        std::string result = "\"";
        for (unsigned char character : value)
        {
            if (character == '\\' || character == '"') result += '\\';
            if (character == '\n') result += "\\n";
            else if (character == '\r') result += "\\r";
            else if (character == '\t') result += "\\t";
            else if (character >= 32) result += static_cast<char>(character);
        }
        return result + '"';
    }

    void Log(sl::LogType type, const char* message)
    {
        // SDK callbacks may arrive on other threads; do not acquire the probe lock.
        OutputDebugStringA(message);
        if (type == sl::LogType::eError && message)
        {
            std::lock_guard<std::mutex> guard(logMutex);
            lastSdkError.assign(message, std::min<size_t>(std::strlen(message), 4096));
        }
    }

    std::string SdkError()
    {
        std::lock_guard<std::mutex> guard(logMutex);
        return lastSdkError;
    }

    template<typename T> bool Resolve(T*& function, const char* name)
    {
        function = reinterpret_cast<T*>(GetProcAddress(interposer, name));
        return function != nullptr;
    }

    bool LoadSdk()
    {
        if (interposer) return true;
        HMODULE self = nullptr;
        if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(&LoadSdk), &self)) return false;
        wchar_t path[32768]{};
        GetModuleFileNameW(self, path, static_cast<DWORD>(std::size(path)));
        libraryDirectory = std::filesystem::path(path).parent_path() / L"Streamline~";
        auto dll = libraryDirectory / L"sl.interposer.dll";
        if (!std::filesystem::exists(dll)) { lastLog = "Missing Streamline~/sl.interposer.dll"; return false; }
        if (!sl::security::verifyEmbeddedSignature(dll.c_str()))
        { lastLog = "NVIDIA interposer signature verification failed"; return false; }
        interposer = LoadLibraryExW(dll.c_str(), nullptr, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        if (!interposer) { lastLog = "LoadLibraryExW failed: " + std::to_string(GetLastError()); return false; }
        if (!Resolve(initialize, "slInit") || !Resolve(shutdown, "slShutdown") ||
            !Resolve(requirements, "slGetFeatureRequirements") || !Resolve(supported, "slIsFeatureSupported") ||
            !Resolve(setDevice, "slSetD3DDevice") || !Resolve(getFunction, "slGetFeatureFunction") ||
            !Resolve(newFrameToken, "slGetNewFrameToken") || !Resolve(setConstants, "slSetConstants") ||
            !Resolve(setTags, "slSetTagForFrame") || !Resolve(evaluate, "slEvaluateFeature") ||
            !Resolve(freeResources, "slFreeResources"))
        {
            lastLog = "Streamline ABI exports missing";
            FreeLibrary(interposer);
            interposer = nullptr;
            return false;
        }
        return true;
    }

    sl::Result InitializeSdk(sl::RenderAPI api, bool manualHooking)
    {
        if (sdkFaulted) return sl::Result::eErrorInvalidState;
        if (initialized) return sl::Result::eOk;
        if (!LoadSdk()) return sl::Result::eErrorIO;
        const sl::Feature features[] = {sl::kFeatureDLSS, sl::kFeatureReflex, sl::kFeatureDLSS_G, sl::kFeatureDLSS_RR};
        const auto directory = libraryDirectory.wstring();
        const wchar_t* paths[] = {directory.c_str()};
        sl::Preferences preferences{};
        preferences.pathsToPlugins = paths;
        preferences.numPathsToPlugins = 1;
        preferences.featuresToLoad = features;
        preferences.numFeaturesToLoad = static_cast<uint32_t>(std::size(features));
        preferences.engine = sl::EngineType::eUnity;
        preferences.engineVersion = "6000.3.15f1";
        preferences.projectId = "ba5b351a-0277-4cd5-a782-c8205a32f914";
        preferences.flags = sl::PreferenceFlags::eDisableCLStateTracking | sl::PreferenceFlags::eUseFrameBasedResourceTagging;
        if (manualHooking) preferences.flags |= sl::PreferenceFlags::eUseManualHooking;
        preferences.renderAPI = api;
        preferences.logLevel = sl::LogLevel::eVerbose;
        preferences.logMessageCallback = Log;
        const auto logDirectory = (libraryDirectory / L"logs").wstring();
        std::error_code logError;
        std::filesystem::create_directories(logDirectory, logError);
        if (!logError) preferences.pathToLogsAndData = logDirectory.c_str();
        {
            std::lock_guard<std::mutex> guard(logMutex);
            lastSdkError.clear();
        }
        const auto result = initialize(preferences, sl::kSDKVersion);
        initialized = result == sl::Result::eOk;
        if (result == sl::Result::eErrorExceptionHandler || result == sl::Result::eErrorInitNotCalled) sdkFaulted = true;
        return result;
    }

    VKAPI_ATTR VkResult VKAPI_CALL ObserveQueuePresent(VkQueue queue, const VkPresentInfoKHR* info)
    {
        const auto result = sdkQueuePresent(queue, info);
        if (result == VK_SUCCESS || result == VK_SUBOPTIMAL_KHR) ++vulkanPresentCount;
        return result;
    }

    VKAPI_ATTR PFN_vkVoidFunction VKAPI_CALL ForwardDeviceProc(VkDevice device, const char* name)
    {
        auto function = sdkGetDeviceProcAddr(device, name);
        if (function && std::strcmp(name, "vkQueuePresentKHR") == 0)
        {
            sdkQueuePresent = reinterpret_cast<PFN_vkQueuePresentKHR>(function);
            return reinterpret_cast<PFN_vkVoidFunction>(ObserveQueuePresent);
        }
        return function;
    }

    VKAPI_ATTR PFN_vkVoidFunction VKAPI_CALL ForwardInstanceProc(VkInstance instance, const char* name)
    {
        auto function = sdkGetInstanceProcAddr(instance, name);
        if (function && std::strcmp(name, "vkGetDeviceProcAddr") == 0)
            return reinterpret_cast<PFN_vkVoidFunction>(ForwardDeviceProc);
        if (function && std::strcmp(name, "vkQueuePresentKHR") == 0)
        {
            sdkQueuePresent = reinterpret_cast<PFN_vkQueuePresentKHR>(function);
            return reinterpret_cast<PFN_vkVoidFunction>(ObserveQueuePresent);
        }
        return function;
    }

    PFN_vkGetInstanceProcAddr UNITY_INTERFACE_API ObserveVulkanInitialization(PFN_vkGetInstanceProcAddr original, void*)
    {
        std::lock_guard<std::recursive_mutex> guard(stateMutex);
        vulkanInitializationObserved = true;
        // The integration probe opts in explicitly. Ordinary projects keep their original loader.
        if (std::wstring(GetCommandLineW()).find(L"-streamline-interpose") != std::wstring::npos)
        {
            if (InitializeSdk(sl::RenderAPI::eVulkan, false) == sl::Result::eOk)
            {
                sdkGetInstanceProcAddr = reinterpret_cast<PFN_vkGetInstanceProcAddr>(GetProcAddress(interposer, "vkGetInstanceProcAddr"));
                sdkGetDeviceProcAddr = reinterpret_cast<PFN_vkGetDeviceProcAddr>(GetProcAddress(interposer, "vkGetDeviceProcAddr"));
                if (sdkGetInstanceProcAddr && sdkGetDeviceProcAddr)
                {
                    vulkanInterposerActive = true;
                    return ForwardInstanceProc;
                }
            }
        }
        return original;
    }

    void Probe()
    {
        std::lock_guard<std::recursive_mutex> guard(stateMutex);
        if (!graphics) return;
        const auto renderer = graphics->GetRenderer();
        if (renderer != kUnityGfxRendererD3D12 && renderer != kUnityGfxRendererVulkan)
        {
            report = "{\"schemaVersion\":1,\"state\":\"UnsupportedApi\",\"renderer\":" + std::to_string(renderer) + ",\"features\":[]}";
            return;
        }
        if (!LoadSdk())
        {
            report = "{\"schemaVersion\":1,\"state\":\"SdkUnavailable\",\"reason\":" + Quote(lastLog) + ",\"features\":[]}";
            return;
        }

        const sl::Feature features[] = {sl::kFeatureDLSS, sl::kFeatureReflex, sl::kFeatureDLSS_G, sl::kFeatureDLSS_RR};
        const auto initializationResult = InitializeSdk(renderer == kUnityGfxRendererD3D12 ? sl::RenderAPI::eD3D12 : sl::RenderAPI::eVulkan, true);
        std::ostringstream output;
        output << "{\"schemaVersion\":1,\"state\":" << Quote(initialized ? "RequirementsProbed" : "InitializationFailed")
            << ",\"sdkVersion\":\"2.14.1\",\"renderer\":" << renderer
            << ",\"initializationResult\":" << static_cast<int>(initializationResult)
            << ",\"vulkanInterceptRegistered\":" << (vulkanInterceptRegistered ? "true" : "false")
            << ",\"vulkanInitializationObserved\":" << (vulkanInitializationObserved ? "true" : "false");
        output << ",\"vulkanInterposerActive\":" << (vulkanInterposerActive ? "true" : "false")
            << ",\"vulkanPresentCount\":" << vulkanPresentCount.load();
        sl::AdapterInfo adapter{};
        LUID luid{};
        bool hasDevice = false;
        bool hasSwapchain = false;
        bool hasCommandList = false;
        if (renderer == kUnityGfxRendererD3D12)
        {
            auto* api = interfaces->Get<IUnityGraphicsD3D12v8>();
            if (api && api->GetDevice())
            {
                hasDevice = true;
                luid = api->GetDevice()->GetAdapterLuid();
                adapter.deviceLUID = reinterpret_cast<uint8_t*>(&luid);
                adapter.deviceLUIDSizeInBytes = sizeof(luid);
                hasSwapchain = api->GetSwapChain() != nullptr;
                UnityGraphicsD3D12RecordingState recording{};
                hasCommandList = api->CommandRecordingState(&recording);
                if (initialized && deviceBindingResult == sl::Result::eErrorDeviceNotCreated)
                    deviceBindingResult = setDevice(api->GetDevice());
            }
        }
        else
        {
            auto* api = interfaces->Get<IUnityGraphicsVulkanV2>();
            if (api)
            {
                const auto instance = api->Instance();
                hasDevice = instance.device != VK_NULL_HANDLE;
                adapter.vkPhysicalDevice = instance.physicalDevice;
                UnityVulkanRecordingState recording{};
                hasCommandList = api->CommandRecordingState(&recording, kUnityVulkanGraphicsQueueAccess_DontCare);
            }
        }
        output << ",\"deviceBindingResult\":";
        if (renderer == kUnityGfxRendererD3D12) output << static_cast<int>(deviceBindingResult);
        else output << "null"; // slSetD3DDevice does not apply to Vulkan.
        output << ",\"deviceAvailable\":" << (hasDevice ? "true" : "false")
            << ",\"swapchainAvailable\":" << (hasSwapchain ? "true" : "false")
            << ",\"commandListAvailable\":" << (hasCommandList ? "true" : "false")
            << ",\"renderingEnabled\":false,\"features\":[";
        if (initialized && hasDevice)
        {
            for (size_t index = 0; index < std::size(features); ++index)
            {
                sl::FeatureRequirements needed{};
                const auto query = requirements(features[index], needed);
                const auto support = supported(features[index], adapter);
                if (index) output << ',';
                output << "{\"featureId\":" << features[index] << ",\"requirementsResult\":" << static_cast<int>(query)
                    << ",\"supportResult\":" << static_cast<int>(support)
                    << ",\"flags\":" << static_cast<uint32_t>(needed.flags)
                    << ",\"requiredVulkanDeviceExtensions\":[";
                for (uint32_t extension = 0; extension < needed.vkNumDeviceExtensions; ++extension)
                {
                    if (extension) output << ',';
                    output << Quote(needed.vkDeviceExtensions[extension]);
                }
                output << "]}";
            }
        }
        output << "],\"reason\":\"Capability probe only; frame resources and presentation lifecycle not integrated\"}";
        report = output.str();
    }

    void UNITY_INTERFACE_API OnResourceProbe(int id, void* texture)
    {
        if (id != eventId + 4) return;
        std::lock_guard<std::recursive_mutex> guard(stateMutex);
        bool accessed = false, recording = false;
        uint32_t width = 0, height = 0, format = 0;
        if (texture && graphics && graphics->GetRenderer() == kUnityGfxRendererVulkan)
        {
            auto* api = interfaces->Get<IUnityGraphicsVulkanV2>();
            UnityVulkanImage image{};
            if (api && api->AccessTexture(texture, UnityVulkanWholeImage, VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT, VK_ACCESS_SHADER_READ_BIT, kUnityVulkanResourceAccess_PipelineBarrier, &image))
            {
                accessed = image.image != VK_NULL_HANDLE;
                width = image.extent.width;
                height = image.extent.height;
                format = static_cast<uint32_t>(image.format);
                UnityVulkanRecordingState state{};
                recording = api->CommandRecordingState(&state, kUnityVulkanGraphicsQueueAccess_DontCare) && state.commandBuffer != VK_NULL_HANDLE;
            }
        }
        else if (texture && graphics && graphics->GetRenderer() == kUnityGfxRendererD3D12)
        {
            auto* api = interfaces->Get<IUnityGraphicsD3D12v8>();
            if (api)
            {
                auto* resource = static_cast<ID3D12Resource*>(texture);
                auto desc = resource->GetDesc();
                api->RequestResourceState(resource, D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE);
                api->NotifyResourceState(resource, D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE, false);
                accessed = true;
                width = static_cast<uint32_t>(desc.Width);
                height = desc.Height;
                format = static_cast<uint32_t>(desc.Format);
                UnityGraphicsD3D12RecordingState state{};
                recording = api->CommandRecordingState(&state) && state.commandList;
            }
        }
        std::ostringstream output;
        output << "{\"state\":" << Quote(accessed && recording ? "ResourceAccessible" : "ResourceUnavailable")
            << ",\"width\":" << width << ",\"height\":" << height << ",\"nativeFormat\":" << format
            << ",\"commandBufferAvailable\":" << (recording ? "true" : "false") << "}";
        resourceReport = output.str();
    }

    bool IsDlssMode(uint32_t mode)
    {
        return (mode >= static_cast<uint32_t>(sl::DLSSMode::eMaxPerformance) &&
            mode <= static_cast<uint32_t>(sl::DLSSMode::eUltraPerformance)) || mode == static_cast<uint32_t>(sl::DLSSMode::eDLAA);
    }

    sl::Result InitializeDlssBackend(const char*& stage)
    {
        if (!graphics || (graphics->GetRenderer() != kUnityGfxRendererD3D12 && graphics->GetRenderer() != kUnityGfxRendererVulkan))
            return sl::Result::eErrorMissingOrInvalidAPI;
        const bool isVulkan = graphics->GetRenderer() == kUnityGfxRendererVulkan;
        stage = "Initialize";
        // Vulkan must have intercepted instance/device creation before Unity initialized graphics.
        if (isVulkan && !vulkanInterposerActive) return sl::Result::eErrorInvalidIntegration;
        auto result = InitializeSdk(isVulkan ? sl::RenderAPI::eVulkan : sl::RenderAPI::eD3D12, !isVulkan);
        if (result != sl::Result::eOk) return result;
        auto* api = isVulkan ? nullptr : interfaces->Get<IUnityGraphicsD3D12v8>();
        auto* vkApi = isVulkan ? interfaces->Get<IUnityGraphicsVulkanV2>() : nullptr;
        if (isVulkan)
        {
            if (!vkApi || !vkApi->Instance().device) return sl::Result::eErrorDeviceNotCreated;
        }
        else
        {
            if (!api || !api->GetDevice()) return sl::Result::eErrorDeviceNotCreated;
            stage = "BindDevice";
            if (deviceBindingResult == sl::Result::eErrorDeviceNotCreated) deviceBindingResult = setDevice(api->GetDevice());
            if (deviceBindingResult != sl::Result::eOk) return deviceBindingResult;
        }
        if (!setDlssOptions)
        {
            stage = "ImportDlss";
            result = getFunction(sl::kFeatureDLSS, "slDLSSSetOptions", reinterpret_cast<void*&>(setDlssOptions));
            if (result != sl::Result::eOk) return result;
        }

        return sl::Result::eOk;
    }

    sl::Result QueryOptimalSettings(const DlssFrame& frame, sl::DLSSOptimalSettings& settings, const char*& stage)
    {
        stage = "ValidateSettings";
        if (!IsDlssMode(frame.mode) || !frame.outputWidth || !frame.outputHeight) return sl::Result::eErrorInvalidParameter;
        auto result = InitializeDlssBackend(stage);
        if (result != sl::Result::eOk) return result;
        PFun_slDLSSGetOptimalSettings* query = nullptr;
        stage = "ImportOptimalSettings";
        result = getFunction(sl::kFeatureDLSS, "slDLSSGetOptimalSettings", reinterpret_cast<void*&>(query));
        if (result != sl::Result::eOk) return result;
        sl::DLSSOptions options{};
        options.mode = static_cast<sl::DLSSMode>(frame.mode);
        options.outputWidth = frame.outputWidth;
        options.outputHeight = frame.outputHeight;
        options.colorBuffersHDR = sl::eTrue;
        options.useAutoExposure = sl::eTrue;
        stage = "OptimalSettings";
        return query(options, settings);
    }
    sl::Result EvaluateDlss(const DlssFrame& frame, const char*& stage)
    {
        stage = "Validate";
        if (!graphics || (graphics->GetRenderer() != kUnityGfxRendererD3D12 && graphics->GetRenderer() != kUnityGfxRendererVulkan))
            return sl::Result::eErrorMissingOrInvalidAPI;
        const bool isVulkan = graphics->GetRenderer() == kUnityGfxRendererVulkan;
        if (!frame.color || !frame.depth || !frame.motion || !frame.output || frame.color == frame.output ||
            !frame.inputWidth || !frame.inputHeight || !frame.outputWidth || !frame.outputHeight ||
            !IsDlssMode(frame.mode))
            return sl::Result::eErrorInvalidParameter;

        auto result = InitializeDlssBackend(stage);
        if (result != sl::Result::eOk) return result;
        auto* api = isVulkan ? nullptr : interfaces->Get<IUnityGraphicsD3D12v8>();
        auto* vkApi = isVulkan ? interfaces->Get<IUnityGraphicsVulkanV2>() : nullptr;
        stage = "ReleaseViewportBeforeReconfigure";
        auto configured = viewportConfigurations.find(frame.viewport);
        if (configured != viewportConfigurations.end() &&
            (configured->second.mode != frame.mode || configured->second.width != frame.outputWidth || configured->second.height != frame.outputHeight))
            return sl::Result::eErrorInvalidIntegration;
        stage = "SetOptions";
        sl::ViewportHandle viewport(frame.viewport);
        sl::DLSSOptions options{};
        options.mode = static_cast<sl::DLSSMode>(frame.mode);
        options.outputWidth = frame.outputWidth;
        options.outputHeight = frame.outputHeight;
        options.colorBuffersHDR = sl::eTrue;
        options.useAutoExposure = sl::eTrue;
        result = setDlssOptions(viewport, options);
        if (result != sl::Result::eOk) return result;
        activeViewports.insert(frame.viewport);
        viewportConfigurations[frame.viewport] = {frame.mode, frame.outputWidth, frame.outputHeight};

        stage = "FrameToken";
        sl::FrameToken* token = nullptr;
        // Camera history indices restart when the managed session changes. Vulkan keeps SL alive
        // across those sessions (and Editor domain reloads), so let SL allocate unique frame IDs.
        result = newFrameToken(token, nullptr);
        if (result != sl::Result::eOk) return result;
        sl::Constants constants{};
        std::memcpy(&constants.cameraViewToClip, frame.cameraViewToClip, sizeof(frame.cameraViewToClip));
        std::memcpy(&constants.clipToCameraView, frame.clipToCameraView, sizeof(frame.clipToCameraView));
        std::memcpy(&constants.clipToPrevClip, frame.clipToPreviousClip, sizeof(frame.clipToPreviousClip));
        std::memcpy(&constants.prevClipToClip, frame.previousClipToClip, sizeof(frame.previousClipToClip));
        constants.jitterOffset = {frame.jitterX, frame.jitterY};
        constants.mvecScale = {frame.motionScaleX, frame.motionScaleY};
        constants.cameraPos = {frame.cameraPosition[0], frame.cameraPosition[1], frame.cameraPosition[2]};
        constants.cameraUp = {frame.cameraUp[0], frame.cameraUp[1], frame.cameraUp[2]};
        constants.cameraRight = {frame.cameraRight[0], frame.cameraRight[1], frame.cameraRight[2]};
        constants.cameraFwd = {frame.cameraForward[0], frame.cameraForward[1], frame.cameraForward[2]};
        constants.cameraNear = frame.nearPlane;
        constants.cameraFar = frame.farPlane;
        constants.cameraFOV = frame.verticalFov;
        constants.cameraAspectRatio = frame.aspectRatio;
        constants.depthInverted = frame.depthInverted ? sl::eTrue : sl::eFalse;
        constants.cameraMotionIncluded = sl::eTrue;
        constants.motionVectors3D = sl::eFalse;
        constants.reset = frame.reset ? sl::eTrue : sl::eFalse;
        constants.orthographicProjection = sl::eFalse;
        constants.motionVectorsDilated = sl::eFalse;
        constants.motionVectorsJittered = sl::eFalse;
        stage = "Constants";
        result = setConstants(constants, *token, viewport);
        if (result != sl::Result::eOk) return result;

        auto* color = static_cast<ID3D12Resource*>(frame.color);
        auto* depth = static_cast<ID3D12Resource*>(frame.depth);
        auto* motion = static_cast<ID3D12Resource*>(frame.motion);
        auto* output = static_cast<ID3D12Resource*>(frame.output);
        const auto readState = D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE;
        sl::Resource colorResource(sl::ResourceType::eTex2d, color, readState);
        sl::Resource depthResource(sl::ResourceType::eTex2d, depth, readState);
        sl::Resource motionResource(sl::ResourceType::eTex2d, motion, readState);
        sl::Resource outputResource(sl::ResourceType::eTex2d, output, D3D12_RESOURCE_STATE_UNORDERED_ACCESS);
        void* commandBuffer = nullptr;
        if (isVulkan)
        {
            void* textures[] = {frame.color, frame.depth, frame.motion, frame.output};
            sl::Resource* resources[] = {&colorResource, &depthResource, &motionResource, &outputResource};
            stage = "VulkanResources";
            for (size_t slot = 0; slot < std::size(textures); ++slot)
            {
                result = PrepareVulkanResource(vkApi, textures[slot], slot == 3, frame.viewport, slot, *resources[slot]);
                if (result != sl::Result::eOk) return result;
            }
            // AccessTexture may change the recording command buffer; query only after all barriers.
            UnityVulkanRecordingState recording{};
            stage = "VulkanCommandBuffer";
            if (!vkApi->CommandRecordingState(&recording, kUnityVulkanGraphicsQueueAccess_DontCare) ||
                !recording.commandBuffer || recording.renderPass) return sl::Result::eErrorInvalidIntegration;
            commandBuffer = reinterpret_cast<void*>(recording.commandBuffer);
        }
        else
        {
            api->RequestResourceState(color, readState);
            api->RequestResourceState(depth, readState);
            api->RequestResourceState(motion, readState);
            api->RequestResourceState(output, D3D12_RESOURCE_STATE_UNORDERED_ACCESS);
            UnityGraphicsD3D12RecordingState recording{};
            stage = "CommandList";
            if (!api->CommandRecordingState(&recording) || !recording.commandList) return sl::Result::eErrorInvalidIntegration;
            commandBuffer = recording.commandList;
        }
        sl::Extent inputExtent{0, 0, frame.inputWidth, frame.inputHeight};
        sl::Extent outputExtent{0, 0, frame.outputWidth, frame.outputHeight};
        sl::ResourceTag tags[] =
        {
            {&colorResource, sl::kBufferTypeScalingInputColor, sl::eValidUntilEvaluate, &inputExtent},
            {&depthResource, sl::kBufferTypeDepth, sl::eValidUntilEvaluate, &inputExtent},
            {&motionResource, sl::kBufferTypeMotionVectors, sl::eValidUntilEvaluate, &inputExtent},
            {&outputResource, sl::kBufferTypeScalingOutputColor, sl::eValidUntilEvaluate, &outputExtent}
        };
        stage = "TagResources";
        result = setTags(*token, viewport, tags, static_cast<uint32_t>(std::size(tags)), commandBuffer);
        if (result == sl::Result::eOk)
        {
            stage = "Evaluate";
            const sl::BaseStructure* inputs[] = {&viewport};
            result = evaluate(sl::kFeatureDLSS, *token, inputs, 1, commandBuffer);
        }
        // SL restores the tagged resource states after its internal transitions.
        // Mark the UAV write so Unity inserts the required dependency for readback/blit.
        if (api)
        {
            api->NotifyResourceState(color, readState, false);
            api->NotifyResourceState(depth, readState, false);
            api->NotifyResourceState(motion, readState, false);
            api->NotifyResourceState(output, D3D12_RESOURCE_STATE_UNORDERED_ACCESS, true);
        }
        return result;
    }

    void EndValidationSession()
    {
        // Caller must have awaited a GPU fence covering all evaluations before this event.
        sl::Result releaseResult = sl::Result::eOk;
        sl::Result shutdownResult = sdkFaulted ? sl::Result::eErrorInvalidState : sl::Result::eOk;
        if (initialized && !sdkFaulted)
        {
            for (uint32_t viewport : activeViewports)
            {
                auto result = freeResources(sl::kFeatureDLSS, sl::ViewportHandle(viewport));
                if (result == sl::Result::eOk) ReleaseVulkanViews(viewport);
                if (result != sl::Result::eOk) releaseResult = result;
                if (result == sl::Result::eErrorExceptionHandler) { sdkFaulted = true; break; }
            }
            if (sdkFaulted) shutdownResult = sl::Result::eErrorInvalidState;
            else if (!vulkanInterposerActive)
            {
                shutdownResult = shutdown();
                if (shutdownResult != sl::Result::eOk) sdkFaulted = true;
                // A failed shutdown must not be represented as an uninitialized SDK.
                if (shutdownResult == sl::Result::eOk)
                {
                    initialized = false;
                    deviceBindingResult = sl::Result::eErrorDeviceNotCreated;
                }
            }
        }
        activeViewports.clear();
        viewportConfigurations.clear();
        frameRequests.clear();
        setDlssOptions = nullptr;
        const auto result = shutdownResult != sl::Result::eOk ? shutdownResult : releaseResult;
        std::ostringstream output;
        output << "{\"state\":\"SessionEnded\",\"stage\":\"Cleanup\",\"result\":" << static_cast<int>(result)
            << ",\"releaseResult\":" << static_cast<int>(releaseResult) << ",\"shutdownResult\":" << static_cast<int>(shutdownResult)
            << ",\"sdkStillInitialized\":" << (initialized ? "true" : "false")
            << ",\"restartRequired\":" << (sdkFaulted ? "true" : "false") << ",\"sdkError\":" << Quote(SdkError()) << "}";
        frameReport = output.str();
    }

    void UNITY_INTERFACE_API OnDlssEvent(int id, void* data)
    {
        std::lock_guard<std::recursive_mutex> guard(stateMutex);
        if (id == eventId + 2) { EndValidationSession(); return; }
        if (id == eventId + 3)
        {
            const auto viewport = static_cast<uint32_t>(reinterpret_cast<uintptr_t>(data) - 1);
            auto result = sdkFaulted ? sl::Result::eErrorInvalidState : sl::Result::eOk;
            // The host must await all GPU work for this viewport before releasing it.
            // This avoids SL's Present-dependent delayed release when options change.
            if (result == sl::Result::eOk && initialized && activeViewports.count(viewport))
                result = freeResources(sl::kFeatureDLSS, sl::ViewportHandle(viewport));
            if (result == sl::Result::eOk)
            {
                ReleaseVulkanViews(viewport);
                activeViewports.erase(viewport);
                viewportConfigurations.erase(viewport);
            }
            if (result == sl::Result::eErrorExceptionHandler) sdkFaulted = true;
            frameReport = "{\"state\":\"ViewportReleased\",\"viewport\":" + std::to_string(viewport)
                + ",\"result\":" + std::to_string(static_cast<int>(result)) + ",\"sdkError\":" + Quote(SdkError()) + "}";
            return;
        }
        if (id != eventId + 1 && id != eventId + 5) return;
        auto request = reinterpret_cast<uintptr_t>(data);
        auto entry = frameRequests.find(request);
        if (entry == frameRequests.end()) return;
        const auto frame = entry->second;
        frameRequests.erase(entry);
        const char* stage = "Exception";
        sl::Result result = sl::Result::eErrorExceptionHandler;
        if (id == eventId + 5)
        {
            sl::DLSSOptimalSettings settings{};
            try { result = QueryOptimalSettings(frame, settings, stage); }
            catch (const std::exception& error) { lastLog = error.what(); }
            if (result == sl::Result::eErrorExceptionHandler) sdkFaulted = true;
            std::ostringstream output;
            output << "{\"requestId\":" << request << ",\"state\":" << Quote(result == sl::Result::eOk ? "OptimalSettings" : "Failed")
                << ",\"result\":" << static_cast<int>(result) << ",\"stage\":" << Quote(stage)
                << ",\"mode\":" << frame.mode << ",\"outputWidth\":" << frame.outputWidth << ",\"outputHeight\":" << frame.outputHeight
                << ",\"optimalWidth\":" << settings.optimalRenderWidth << ",\"optimalHeight\":" << settings.optimalRenderHeight
                << ",\"minWidth\":" << settings.renderWidthMin << ",\"minHeight\":" << settings.renderHeightMin
                << ",\"maxWidth\":" << settings.renderWidthMax << ",\"maxHeight\":" << settings.renderHeightMax
                << ",\"sdkError\":" << Quote(SdkError()) << "}";
            frameReport = output.str();
            return;
        }
        try { result = EvaluateDlss(frame, stage); }
        catch (const std::exception& error) { lastLog = error.what(); }
        if (result == sl::Result::eErrorExceptionHandler) sdkFaulted = true;
        std::ostringstream output;
        output << "{\"requestId\":" << request << ",\"state\":" << Quote(result == sl::Result::eOk ? "Evaluated" : "Failed")
            << ",\"result\":" << static_cast<int>(result) << ",\"stage\":" << Quote(stage)
            << ",\"inputWidth\":" << frame.inputWidth << ",\"inputHeight\":" << frame.inputHeight
            << ",\"outputWidth\":" << frame.outputWidth << ",\"outputHeight\":" << frame.outputHeight
            << ",\"presentationValidated\":false,\"sdkError\":" << Quote(SdkError()) << "}";
        frameReport = output.str();
    }

    void UNITY_INTERFACE_API OnRenderEvent(int id)
    {
        if (id != eventId) return;
        try { Probe(); }
        catch (const std::exception& error)
        {
            std::lock_guard<std::recursive_mutex> guard(stateMutex);
            report = "{\"schemaVersion\":1,\"state\":\"ProbeFailed\",\"reason\":" + Quote(error.what()) + ",\"features\":[]}";
        }
    }

    void UNITY_INTERFACE_API OnDeviceEvent(UnityGfxDeviceEventType type)
    {
        std::lock_guard<std::recursive_mutex> guard(stateMutex);
        if (type == kUnityGfxDeviceEventInitialize && graphics && graphics->GetRenderer() == kUnityGfxRendererD3D12)
        {
            if (auto* d3d = interfaces->Get<IUnityGraphicsD3D12v8>(); d3d && d3d->GetDevice())
            {
                UnityD3D12PluginEventConfig config{};
                config.graphicsQueueAccess = kUnityD3D12GraphicsQueueAccess_DontCare;
                config.flags = kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState;
                d3d->ConfigureEvent(eventId + 1, &config);
                d3d->ConfigureEvent(eventId + 2, &config);
                d3d->ConfigureEvent(eventId + 3, &config);
                d3d->ConfigureEvent(eventId + 4, &config);
                d3d->ConfigureEvent(eventId + 5, &config);
            }
        }
        if (type == kUnityGfxDeviceEventInitialize && graphics && graphics->GetRenderer() == kUnityGfxRendererVulkan)
        {
            if (auto* vulkan = interfaces->Get<IUnityGraphicsVulkanV2>())
            {
                UnityVulkanPluginEventConfig config{};
                config.renderPassPrecondition = kUnityVulkanRenderPass_EnsureOutside;
                config.graphicsQueueAccess = kUnityVulkanGraphicsQueueAccess_DontCare;
                config.flags = kUnityVulkanEventConfigFlag_EnsurePreviousFrameSubmission | kUnityVulkanEventConfigFlag_ModifiesCommandBuffersState;
                for (int offset = 0; offset < 6; ++offset) vulkan->ConfigureEvent(eventId + offset, &config);
            }
        }
        if (type == kUnityGfxDeviceEventShutdown && initialized && !sdkFaulted)
        {
            const auto result = shutdown();
            if (result != sl::Result::eOk)
            {
                sdkFaulted = true;
                report = R"({"schemaVersion":1,"state":"ShutdownFailed","restartRequired":true,"features":[]})";
                return;
            }
            initialized = false;
            deviceBindingResult = sl::Result::eErrorDeviceNotCreated;
            vulkanInterposerActive = false;
            while (!viewportViews.empty()) ReleaseVulkanViews(viewportViews.begin()->first);
            activeViewports.clear();
            viewportConfigurations.clear();
            frameRequests.clear();
            setDlssOptions = nullptr;
            report = R"({"schemaVersion":1,"state":"DeviceShutdown","features":[]})";
        }
    }
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* value)
{
    interfaces = value;
    graphics = value->Get<IUnityGraphics>();
    if (!graphics) return;
    eventId = graphics->ReserveEventIDRange(6);
    graphics->RegisterDeviceEventCallback(OnDeviceEvent);
    if (auto* vulkan = value->Get<IUnityGraphicsVulkanV2>())
        vulkanInterceptRegistered = vulkan->InterceptInitialization(ObserveVulkanInitialization, nullptr);
    // Covers late Editor imports; preloaded plugins receive the real initialize event later.
    if (graphics->GetRenderer() == kUnityGfxRendererD3D12 || graphics->GetRenderer() == kUnityGfxRendererVulkan) OnDeviceEvent(kUnityGfxDeviceEventInitialize);
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
    if (interfaces)
        if (auto* vulkan = interfaces->Get<IUnityGraphicsVulkanV2>())
            if (vulkanInterceptRegistered) vulkan->RemoveInterceptInitialization(ObserveVulkanInitialization);
    if (graphics) graphics->UnregisterDeviceEventCallback(OnDeviceEvent);
    OnDeviceEvent(kUnityGfxDeviceEventShutdown);
    if (interposer) FreeLibrary(interposer);
    interposer = nullptr;
    graphics = nullptr;
    interfaces = nullptr;
}

extern "C" UnityRenderingEvent UNITY_INTERFACE_EXPORT __cdecl SleepyStreamlineGetProbeEvent() { return OnRenderEvent; }
extern "C" int UNITY_INTERFACE_EXPORT __cdecl SleepyStreamlineGetProbeEventId() { return eventId; }
extern "C" int UNITY_INTERFACE_EXPORT __cdecl SleepyStreamlineGetReleaseViewportEventId() { return eventId + 3; }
extern "C" int UNITY_INTERFACE_EXPORT __cdecl SleepyStreamlineGetResourceProbeEventId() { return eventId + 4; }
extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT __cdecl SleepyStreamlineGetResourceProbeEvent() { return OnResourceProbe; }
extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT __cdecl SleepyStreamlineGetDlssEvent() { return OnDlssEvent; }
extern "C" int UNITY_INTERFACE_EXPORT __cdecl SleepyStreamlineGetFrameSize() { return sizeof(DlssFrame); }
extern "C" int UNITY_INTERFACE_EXPORT __cdecl SleepyStreamlineGetOptimalSettingsEventId() { return eventId + 5; }
extern "C" uintptr_t UNITY_INTERFACE_EXPORT __cdecl SleepyStreamlineQueueFrame(const DlssFrame* frame)
{
    std::lock_guard<std::recursive_mutex> guard(stateMutex);
    if (!frame || frameRequests.size() >= 16) return 0;
    const auto request = ++nextRequest;
    frameRequests.emplace(request, *frame);
    return request;
}
extern "C" void UNITY_INTERFACE_EXPORT __cdecl SleepyStreamlineCancelFrame(uintptr_t request)
{
    std::lock_guard<std::recursive_mutex> guard(stateMutex);
    frameRequests.erase(request);
}
extern "C" int UNITY_INTERFACE_EXPORT __cdecl SleepyStreamlineCopyFrameReport(char* destination, int capacity)
{
    std::lock_guard<std::recursive_mutex> guard(stateMutex);
    const int required = static_cast<int>(frameReport.size()) + 1;
    if (destination && capacity >= required) std::memcpy(destination, frameReport.c_str(), required);
    return required;
}
extern "C" int UNITY_INTERFACE_EXPORT __cdecl SleepyStreamlineCopyReport(char* destination, int capacity)
{
    std::lock_guard<std::recursive_mutex> guard(stateMutex);
    const int required = static_cast<int>(report.size()) + 1;
    if (destination && capacity >= required) std::memcpy(destination, report.c_str(), required);
    return required;
}
extern "C" int UNITY_INTERFACE_EXPORT __cdecl SleepyStreamlineCopyResourceReport(char* destination, int capacity)
{
    std::lock_guard<std::recursive_mutex> guard(stateMutex);
    const int required = static_cast<int>(resourceReport.size()) + 1;
    if (destination && capacity >= required) std::memcpy(destination, resourceReport.c_str(), required);
    return required;
}
