using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Malco.Launcher
{
    internal static class LauncherDialog
    {
        public static void ShowError(string title, string message, LauncherLanguage language)
        {
            using (var form = CreateShell(title, new Size(480, 210), true))
            {
                var content = new Label
                {
                    Location = new Point(24, 22),
                    Size = new Size(432, 106),
                    Text = message
                };
                var close = new Button
                {
                    DialogResult = DialogResult.OK,
                    Location = new Point(356, 148),
                    Size = new Size(96, 34),
                    Text = language.Close
                };
                form.Controls.Add(content);
                form.Controls.Add(close);
                form.AcceptButton = close;
                form.CancelButton = close;
                form.ShowDialog();
            }
        }

        public static bool ShowConfirmation(
            string title,
            string message,
            LauncherLanguage language,
            bool required)
        {
            using (var form = CreateShell(title, new Size(480, 210), !required))
            {
                var content = new Label
                {
                    Location = new Point(24, 22),
                    Size = new Size(432, 106),
                    Text = message
                };
                var update = new Button
                {
                    DialogResult = DialogResult.OK,
                    Location = new Point(required ? 356 : 260, 148),
                    Size = new Size(96, 34),
                    Text = language.Yes
                };
                form.Controls.Add(content);
                form.Controls.Add(update);
                form.AcceptButton = update;
                if (!required)
                {
                    var decline = new Button
                    {
                        DialogResult = DialogResult.Cancel,
                        Location = new Point(356, 148),
                        Size = new Size(96, 34),
                        Text = language.No
                    };
                    form.Controls.Add(decline);
                    form.CancelButton = decline;
                }
                return form.ShowDialog() == DialogResult.OK;
            }
        }

        public static Exception RunInstallation(
            string title,
            LauncherLanguage language,
            Func<IProgress<UpdateProgress>, ReleaseReference> install)
        {
            Exception error = null;
            var installationCompleted = false;
            using (var form = CreateShell(title, new Size(440, 132), false))
            {
                var status = new Label
                {
                    AutoEllipsis = true,
                    Location = new Point(20, 18),
                    Size = new Size(400, 38),
                    Text = language.ProgressText(UpdateStage.Preparing, 0)
                };
                var progressBar = new ProgressBar
                {
                    Location = new Point(20, 76),
                    Size = new Size(400, 20),
                    Style = ProgressBarStyle.Marquee,
                    MarqueeAnimationSpeed = 28
                };
                form.Controls.Add(status);
                form.Controls.Add(progressBar);
                form.FormClosing += (_, args) =>
                {
                    if (installationCompleted) return;
                    if (args.CloseReason == CloseReason.UserClosing)
                    {
                        args.Cancel = true;
                        return;
                    }
                    error = new OperationCanceledException(
                        "The update window closed before installation completed.");
                };
                form.Shown += async (_, _) =>
                {
                    var progress = new Progress<UpdateProgress>(value =>
                    {
                        if (form.IsDisposed) return;
                        status.Text = language.ProgressText(value.Stage, value.Percentage);
                        if (value.Stage == UpdateStage.Downloading && value.TotalBytes > 0)
                        {
                            progressBar.Style = ProgressBarStyle.Continuous;
                            progressBar.Value = value.Percentage;
                        }
                        else if (value.Stage == UpdateStage.Completed)
                        {
                            progressBar.Style = ProgressBarStyle.Continuous;
                            progressBar.Value = 100;
                        }
                        else
                        {
                            progressBar.Style = ProgressBarStyle.Marquee;
                        }
                    });
                    try
                    {
                        await Task.Run(() => install(progress));
                    }
                    catch (Exception exception)
                    {
                        error = exception;
                    }
                    finally
                    {
                        installationCompleted = true;
                        form.Close();
                    }
                };
                form.ShowDialog();
            }
            return error;
        }

        private static Form CreateShell(string title, Size size, bool controlBox)
        {
            var form = new Form
            {
                Text = title,
                ClientSize = size,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ControlBox = controlBox,
                ShowInTaskbar = true,
                StartPosition = FormStartPosition.CenterScreen,
                AutoScaleMode = AutoScaleMode.Dpi,
                TopMost = true
            };
            form.Shown += (_, _) =>
            {
                form.WindowState = FormWindowState.Normal;
                form.Activate();
                form.BringToFront();
            };
            return form;
        }
    }
}
