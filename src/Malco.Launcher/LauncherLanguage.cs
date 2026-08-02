using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Malco.Launcher
{
    internal sealed class LauncherLanguage
    {
        public const string ProcessEnvironmentName = "MALCO_UI_LANGUAGE";
        private const int MaximumLayoutBytes = 1024 * 1024;
        private const int MaximumInstallerLanguageBytes = 32;

        private LauncherLanguage(bool isKorean)
        {
            IsKorean = isKorean;
        }

        public bool IsKorean { get; }
        public string Code => IsKorean ? "ko-KR" : "en-US";

        public static LauncherLanguage Resolve(string installRoot)
        {
            string configured;
            var dataRoot = Path.Combine(installRoot, "data");
            if (TryReadLayoutLanguage(Path.Combine(dataRoot, "hud-layout.json"), out configured) ||
                TryReadShortText(Path.Combine(dataRoot, "installer-language.txt"), out configured))
            {
                return FromCode(configured);
            }

            return new LauncherLanguage(string.Equals(
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                "ko",
                StringComparison.OrdinalIgnoreCase));
        }

        public string RequiredTitle => IsKorean ? "Malco 필수 업데이트" : "Malco Required Update";
        public string OptionalTitle => IsKorean ? "Malco 업데이트" : "Malco Update";
        public string Yes => IsKorean ? "예" : "Yes";
        public string No => IsKorean ? "아니요" : "No";
        public string Close => IsKorean ? "닫기" : "Close";
        public string UpdateFailed => IsKorean
            ? "업데이트를 설치하지 못했습니다."
            : "The update could not be installed.";

        public string UpdateMessage(bool required, string version)
        {
            if (IsKorean)
            {
                return required
                    ? "필수 업데이트가 있습니다.\r\n\r\n버전 " + version +
                      "을(를) 설치해야 Malco를 사용할 수 있습니다.\r\n지금 업데이트하시겠습니까?"
                    : "업데이트가 있습니다.\r\n\r\n버전 " + version +
                      "을(를) 지금 설치하시겠습니까?\r\n아니요를 선택하면 현재 버전으로 Malco를 실행합니다.";
            }

            return required
                ? "A required update is available.\r\n\r\nVersion " + version +
                  " must be installed before Malco can be used.\r\nUpdate now?"
                : "An update is available.\r\n\r\nInstall version " + version +
                  " now?\r\nChoose No to start the currently installed version.";
        }

        public string ProgressText(UpdateStage stage, int percentage)
        {
            if (IsKorean)
            {
                return stage switch
                {
                    UpdateStage.Preparing => "업데이트를 준비하는 중...",
                    UpdateStage.Downloading => "업데이트 다운로드 중... " + percentage + "%",
                    UpdateStage.Verifying => "다운로드를 검증하고 설치 파일을 준비하는 중...",
                    UpdateStage.Finalizing => "업데이트 적용을 마무리하는 중...",
                    UpdateStage.Completed => "업데이트가 완료되었습니다.",
                    _ => string.Empty
                };
            }

            return stage switch
            {
                UpdateStage.Preparing => "Preparing the update...",
                UpdateStage.Downloading => "Downloading the update... " + percentage + "%",
                UpdateStage.Verifying => "Verifying the download and preparing files...",
                UpdateStage.Finalizing => "Finishing the update...",
                UpdateStage.Completed => "The update is complete.",
                _ => string.Empty
            };
        }

        public string FailureMessage(string key)
        {
            if (IsKorean)
            {
                return key switch
                {
                    "policy" => "Malco 업데이트 정책이 없거나 올바르지 않습니다. Malco를 복구하거나 다시 설치해 주세요.",
                    "state" => "Malco 설치 상태가 없거나 올바르지 않습니다. Malco를 복구하거나 다시 설치해 주세요.",
                    "release" => "검증된 Malco 버전을 시작할 수 없습니다. Malco를 복구하거나 다시 설치해 주세요.",
                    "startup" => "선택한 Malco 버전을 안전하게 시작하지 못했습니다. 남아 있는 Malco 프로세스를 닫고 다시 시도하거나 다시 설치해 주세요.",
                    "required" => "필수 업데이트를 설치하지 못해 Malco를 시작할 수 없습니다. 네트워크 연결과 저장 공간을 확인한 뒤 다시 실행해 주세요.",
                    "unexpected" => "Malco Launcher에서 예상하지 못한 오류가 발생했습니다. Malco를 복구하거나 다시 설치해 주세요.",
                    _ => null
                };
            }

            return key switch
            {
                "policy" => "Malco's update policy is missing or invalid. Repair or reinstall Malco.",
                "state" => "Malco's installed release state is missing or invalid. Repair or reinstall Malco.",
                "release" => "No verified Malco release can be started. Repair or reinstall Malco.",
                "startup" => "The selected Malco release did not start safely. Close any remaining Malco process and retry, or reinstall Malco.",
                "required" => "The required update could not be installed, so Malco cannot start. Check the network connection and available storage, then try again.",
                "unexpected" => "Malco Launcher encountered an unexpected error. Repair or reinstall Malco.",
                _ => null
            };
        }

        private static LauncherLanguage FromCode(string value) =>
            new LauncherLanguage(
                string.Equals(value, "ko-KR", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "ko_KR", StringComparison.OrdinalIgnoreCase));

        private static bool TryReadLayoutLanguage(string path, out string language)
        {
            language = null;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length <= 0 || info.Length > MaximumLayoutBytes) return false;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var document = JsonDocument.Parse(stream))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if (!string.Equals(property.Name, "language", StringComparison.OrdinalIgnoreCase) ||
                            property.Value.ValueKind != JsonValueKind.String) continue;
                        language = property.Value.GetString();
                        return IsSupported(language);
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is JsonException)
            {
            }
            return false;
        }

        private static bool TryReadShortText(string path, out string language)
        {
            language = null;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length <= 0 || info.Length > MaximumInstallerLanguageBytes) return false;
                language = File.ReadAllText(path).Trim();
                return IsSupported(language);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool IsSupported(string value) =>
            string.Equals(value, "en-US", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "en_US", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "ko-KR", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "ko_KR", StringComparison.OrdinalIgnoreCase);
    }
}
