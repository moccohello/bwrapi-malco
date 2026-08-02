using System;
using System.Threading;
using System.Threading.Tasks;

namespace Malco.Launcher
{
    internal sealed class UpdateDialogResult
    {
        public bool Accepted { get; set; }
        public bool Installed { get; set; }
        public Exception Error { get; set; }
    }

    internal sealed class UpdateDialog
    {
        private const int UpdateButtonId = 1001;
        private const int DeclineButtonId = 1002;

        private enum Page
        {
            Prompt,
            Progress,
            Failed
        }

        private readonly VerifiedEnvelope _release;
        private readonly bool _required;
        private readonly LauncherLanguage _language;
        private readonly Func<IProgress<UpdateProgress>, ReleaseReference> _install;
        private readonly UpdateDialogResult _result = new UpdateDialogResult();
        private NativeTaskDialog _dialog;
        private Page _page;
        private int _installationStarted;
        private int _running;

        private UpdateDialog(
            VerifiedEnvelope release,
            bool required,
            LauncherLanguage language,
            Func<IProgress<UpdateProgress>, ReleaseReference> install)
        {
            _release = release ?? throw new ArgumentNullException(nameof(release));
            _required = required;
            _language = language ?? throw new ArgumentNullException(nameof(language));
            _install = install ?? throw new ArgumentNullException(nameof(install));
        }

        public static UpdateDialogResult ShowUpdate(
            VerifiedEnvelope release,
            bool required,
            LauncherLanguage language,
            Func<IProgress<UpdateProgress>, ReleaseReference> install)
        {
            return new UpdateDialog(release, required, language, install).Show();
        }

        private UpdateDialogResult Show()
        {
            using (_dialog = new NativeTaskDialog(HandleNotification))
            using (var prompt = CreatePromptPage())
            {
                _page = Page.Prompt;
                _dialog.Show(prompt);
            }
            return _result;
        }

        private NativeTaskDialog.NativeTaskDialogPage CreatePromptPage()
        {
            var title = _required ? _language.RequiredTitle : _language.OptionalTitle;
            var buttons = _required
                ? new[] { new NativeTaskDialogButton(UpdateButtonId, _language.Yes) }
                : new[]
                {
                    new NativeTaskDialogButton(UpdateButtonId, _language.Yes),
                    new NativeTaskDialogButton(DeclineButtonId, _language.No)
                };
            return _dialog.CreatePage(
                title,
                _language.UpdateMessage(_required, _release.Manifest.Version),
                buttons,
                UpdateButtonId,
                showProgress: false);
        }

        private int HandleNotification(NativeTaskDialogNotification notification, int buttonId)
        {
            try
            {
                if (notification != NativeTaskDialogNotification.ButtonClicked)
                {
                    return NativeTaskDialog.CloseDialog;
                }

                if (Volatile.Read(ref _running) != 0)
                {
                    return NativeTaskDialog.KeepDialogOpen;
                }
                if (_result.Installed)
                {
                    return NativeTaskDialog.CloseDialog;
                }
                if (_page == Page.Failed)
                {
                    return NativeTaskDialog.CloseDialog;
                }
                if (buttonId == DeclineButtonId || buttonId == NativeTaskDialog.CancelButtonId)
                {
                    _result.Accepted = false;
                    return NativeTaskDialog.CloseDialog;
                }
                if (_page != Page.Prompt || buttonId != UpdateButtonId)
                {
                    return NativeTaskDialog.KeepDialogOpen;
                }

                BeginInstallation();
                return NativeTaskDialog.KeepDialogOpen;
            }
            catch (Exception exception)
            {
                _result.Accepted = true;
                _result.Error = exception;
                Volatile.Write(ref _running, 0);
                ShowFailurePage();
                return NativeTaskDialog.KeepDialogOpen;
            }
        }

        private void BeginInstallation()
        {
            if (Interlocked.Exchange(ref _installationStarted, 1) != 0)
            {
                return;
            }

            _result.Accepted = true;
            Volatile.Write(ref _running, 1);
            _page = Page.Progress;
            var progressPage = _dialog.CreatePage(
                _required ? _language.RequiredTitle : _language.OptionalTitle,
                _language.ProgressText(UpdateStage.Preparing, 0),
                new[] { new NativeTaskDialogButton(UpdateButtonId, _language.Yes) },
                UpdateButtonId,
                showProgress: true);
            _dialog.Navigate(progressPage);
            _dialog.EnableButton(UpdateButtonId, false);
            _dialog.ShowMarquee();

            Task.Run(() => Install(new DialogProgress(this)));
        }

        private void Install(IProgress<UpdateProgress> progress)
        {
            try
            {
                _install(progress);
                _result.Installed = true;
                Volatile.Write(ref _running, 0);
                _dialog.Close();
            }
            catch (Exception exception)
            {
                _result.Error = exception;
                Volatile.Write(ref _running, 0);
                ShowFailurePage();
            }
        }

        private void PresentProgress(UpdateProgress progress)
        {
            if (Volatile.Read(ref _running) == 0)
            {
                return;
            }

            _dialog.SetContent(_language.ProgressText(progress.Stage, progress.Percentage));
            if (progress.Stage == UpdateStage.Downloading && progress.TotalBytes > 0)
            {
                _dialog.ShowPercentage(progress.Percentage);
                return;
            }
            if (progress.Stage == UpdateStage.Completed)
            {
                _dialog.ShowPercentage(100);
                return;
            }
            _dialog.ShowMarquee();
        }

        private void ShowFailurePage()
        {
            _page = Page.Failed;
            var failurePage = _dialog.CreatePage(
                _required ? _language.RequiredTitle : _language.OptionalTitle,
                _language.UpdateFailed,
                new[] { new NativeTaskDialogButton(UpdateButtonId, _language.Close) },
                UpdateButtonId,
                showProgress: false);
            _dialog.Navigate(failurePage);
        }

        private sealed class DialogProgress : IProgress<UpdateProgress>
        {
            private readonly UpdateDialog _owner;

            public DialogProgress(UpdateDialog owner)
            {
                _owner = owner;
            }

            public void Report(UpdateProgress value)
            {
                _owner.PresentProgress(value);
            }
        }
    }
}
