using System;

namespace Malco.Launcher
{
    internal sealed class UpdateDialogResult
    {
        public bool Accepted { get; set; }
        public bool Installed { get; set; }
        public Exception Error { get; set; }
    }

    internal static class UpdateDialog
    {
        public static UpdateDialogResult ShowUpdate(
            VerifiedEnvelope release,
            bool required,
            LauncherLanguage language,
            Func<IProgress<UpdateProgress>, ReleaseReference> install)
        {
            if (release == null) throw new ArgumentNullException(nameof(release));
            if (language == null) throw new ArgumentNullException(nameof(language));
            if (install == null) throw new ArgumentNullException(nameof(install));

            var title = required ? language.RequiredTitle : language.OptionalTitle;
            var result = new UpdateDialogResult
            {
                Accepted = LauncherDialog.ShowConfirmation(
                    title,
                    language.UpdateMessage(required, release.Manifest.Version),
                    language,
                    required)
            };
            if (!result.Accepted) return result;

            result.Error = LauncherDialog.RunInstallation(
                title,
                language,
                install);
            result.Installed = result.Error == null;
            if (!result.Installed)
            {
                LauncherDialog.ShowError(
                    title,
                    language.UpdateFailed,
                    language);
            }
            return result;
        }
    }
}
