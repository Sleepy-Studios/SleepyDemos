pipeline {
    agent { label 'sleepydemos-local' }
    options {
        disableConcurrentBuilds()
        skipDefaultCheckout(true)
        timeout(time: 6, unit: 'HOURS')
        buildDiscarder(logRotator(numToKeepStr: '30', artifactNumToKeepStr: '10'))
    }
    parameters {
        string(name: 'SourceRef', defaultValue: 'main', description: '源码分支、标签或提交 SHA；执行时固定提交')
        choice(name: 'Platform', choices: ['PC', 'Android'])
        choice(name: 'BuildKind', choices: ['Full', 'Hotfix'])
        choice(name: 'ResourceMode', choices: ['Offline', 'OnlineFull', 'OnlineDownload'], description: '仅 Full 生效；Hotfix 继承已发布基线')
        choice(name: 'AndroidFormat', choices: ['Apk', 'Aab'], description: '仅 Android Full 生效')
        choice(name: 'AndroidSigning', choices: ['Debug', 'Custom'], description: '仅 Android Full 生效')
        booleanParam(name: 'Development', defaultValue: false, description: 'Hotfix 必须与已发布基线一致')
        string(name: 'PlayerVersion', defaultValue: '', description: 'Full 空值读项目；Hotfix 空值继承基线，填写时须匹配基线')
        string(name: 'AndroidVersionCode', defaultValue: '', description: '仅 Android Full 生效；空值使用本次 BUILD_NUMBER，覆盖值必须是正整数')
        string(name: 'ResourceVersion', defaultValue: '', description: '空值自动生成唯一资源版本')
        booleanParam(name: 'Publish', defaultValue: false)
        choice(name: 'Provider', choices: ['Pages', 'Ssh'])
    }
    environment {
        SLEEPY_MACHINE_CONFIG = "${env.SLEEPY_MACHINE_CONFIG ?: 'E:/Jenkins/machine.json'}"
        SourceRef = "${params.SourceRef}"
        Platform = "${params.Platform}"
        BuildKind = "${params.BuildKind}"
        ResourceMode = "${params.ResourceMode}"
        AndroidFormat = "${params.AndroidFormat}"
        AndroidSigning = "${params.AndroidSigning}"
        Development = "${params.Development}"
        PlayerVersion = "${params.PlayerVersion}"
        AndroidVersionCode = "${params.AndroidVersionCode}"
        ResourceVersion = "${params.ResourceVersion}"
        Publish = "${params.Publish}"
        Provider = "${params.Provider}"
    }
    stages {
        stage('固定源码与配置') {
            steps {
                checkout scm
                withCredentials([string(credentialsId: 'sleepydemos-github', variable: 'GH_TOKEN')]) {
                    powershell '& ./scripts/jenkins/Invoke-Build.ps1 -Stage Checkout -ConfigPath $env:SLEEPY_MACHINE_CONFIG'
                }
            }
        }
        stage('准备与构建') {
            steps {
                script {
                    if (params.Publish) {
                        if (params.Provider == 'Pages') {
                            withCredentials([string(credentialsId: 'sleepydemos-github', variable: 'GH_TOKEN')]) {
                                powershell 'if ([string]::IsNullOrWhiteSpace($env:GH_TOKEN)) { throw "缺少 GitHub 发布凭据。" }'
                            }
                        } else {
                            withCredentials([sshUserPrivateKey(credentialsId: 'sleepydemos-ssh', keyFileVariable: 'SLEEPY_SSH_KEY_FILE', passphraseVariable: 'SLEEPY_SSH_KEY_PASSPHRASE')]) {
                                powershell 'if (-not (Test-Path -LiteralPath $env:SLEEPY_SSH_KEY_FILE)) { throw "缺少 SSH 发布私钥。" }'
                            }
                        }
                    }
                    if (params.Platform == 'Android' && params.BuildKind == 'Full' && params.AndroidSigning == 'Custom') {
                        withCredentials([
                            file(credentialsId: 'sleepydemos-keystore', variable: 'SLEEPY_KEYSTORE_FILE'),
                            string(credentialsId: 'sleepydemos-store-password', variable: 'SLEEPY_KEYSTORE_PASSWORD'),
                            string(credentialsId: 'sleepydemos-key-alias', variable: 'SLEEPY_KEY_ALIAS'),
                            string(credentialsId: 'sleepydemos-key-password', variable: 'SLEEPY_KEY_ALIAS_PASSWORD')
                        ]) {
                            powershell '& ./scripts/jenkins/Invoke-Build.ps1 -Stage Prepare -ConfigPath $env:SLEEPY_MACHINE_CONFIG'
                            powershell '& ./scripts/jenkins/Invoke-Build.ps1 -Stage Build -ConfigPath $env:SLEEPY_MACHINE_CONFIG'
                        }
                    } else {
                        powershell '& ./scripts/jenkins/Invoke-Build.ps1 -Stage Prepare -ConfigPath $env:SLEEPY_MACHINE_CONFIG'
                        powershell '& ./scripts/jenkins/Invoke-Build.ps1 -Stage Build -ConfigPath $env:SLEEPY_MACHINE_CONFIG'
                    }
                }
            }
        }
        stage('发布') {
            when { expression { params.Publish } }
            options { timeout(time: 30, unit: 'MINUTES') }
            steps {
                script {
                    if (params.Provider == 'Pages') {
                        withCredentials([string(credentialsId: 'sleepydemos-github', variable: 'GH_TOKEN')]) {
                            powershell '& ./scripts/jenkins/Invoke-Build.ps1 -Stage Deploy -ConfigPath $env:SLEEPY_MACHINE_CONFIG'
                        }
                    } else {
                        withCredentials([sshUserPrivateKey(credentialsId: 'sleepydemos-ssh', keyFileVariable: 'SLEEPY_SSH_KEY_FILE', passphraseVariable: 'SLEEPY_SSH_KEY_PASSPHRASE')]) {
                            powershell '& ./scripts/jenkins/Invoke-Build.ps1 -Stage Deploy -ConfigPath $env:SLEEPY_MACHINE_CONFIG'
                        }
                    }
                }
            }
        }
        stage('打包归档') {
            steps { powershell '& ./scripts/jenkins/Invoke-Build.ps1 -Stage Package -ConfigPath $env:SLEEPY_MACHINE_CONFIG' }
        }
    }
    post {
        always {
            script {
                try {
                    withEnv(["SLEEPY_JENKINS_RESULT=${currentBuild.currentResult}"]) {
                        powershell '& ./scripts/jenkins/Invoke-Build.ps1 -Stage Finalize -ConfigPath $env:SLEEPY_MACHINE_CONFIG'
                    }
                } finally {
                    archiveArtifacts artifacts: "ci-archive/${env.BUILD_NUMBER}/**", allowEmptyArchive: false, fingerprint: true
                    powershell '& ./scripts/jenkins/Invoke-Build.ps1 -Stage Cleanup -ConfigPath $env:SLEEPY_MACHINE_CONFIG'
                }
            }
        }
    }
}
