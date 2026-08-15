namespace Hotfix.DroneFlight
{
    /// <summary>运行时与双语 Inspector 共享的配置校验结果。</summary>
    internal readonly struct DroneConfigValidationResult
    {
        private DroneConfigValidationResult(bool isValid, string chineseMessage, string englishMessage)
        {
            IsValid = isValid;
            ChineseMessage = chineseMessage;
            EnglishMessage = englishMessage;
        }

        /// 配置是否有效。
        internal bool IsValid { get; }

        /// 中文诊断；有效时为空。
        internal string ChineseMessage { get; }

        /// 英文诊断；有效时为空。
        internal string EnglishMessage { get; }

        internal static DroneConfigValidationResult Valid => new(true, string.Empty, string.Empty);

        /// <summary>
        /// 创建一条稳定的双语失败诊断。
        /// </summary>
        /// <param name="chineseMessage">面向中文 Inspector 和运行时日志的诊断。</param>
        /// <param name="englishMessage">面向英文 Inspector 的等价诊断。</param>
        internal static DroneConfigValidationResult Invalid(string chineseMessage, string englishMessage)
        {
            return new DroneConfigValidationResult(false, chineseMessage, englishMessage);
        }
    }
}
