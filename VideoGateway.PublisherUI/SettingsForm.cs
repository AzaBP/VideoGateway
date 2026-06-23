using System;
using System.IO;
using System.Windows.Forms;
using VideoGateway.Testing.Common;

namespace VideoGateway.PublisherUI
{
    public class SettingsForm : Form
    {
        private TextBox _txtFfmpeg = new TextBox() { Width = 320 };
        private TextBox _txtFfplay = new TextBox() { Width = 320 };
        private TextBox _txtVlc = new TextBox() { Width = 320 };

        public SettingsForm()
        {
            Text = "Configuración - Ejecutables";
            Width = 520; Height = 220;
            StartPosition = FormStartPosition.CenterParent;

            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 3 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            panel.Controls.Add(new Label { Text = "ffmpeg:", AutoSize = true }, 0, 0);
            panel.Controls.Add(_txtFfmpeg, 1, 0);
            var b1 = new Button { Text = "Examinar..." };
            b1.Click += (s, e) => Browse(_txtFfmpeg);
            panel.Controls.Add(b1, 2, 0);

            panel.Controls.Add(new Label { Text = "ffplay:", AutoSize = true }, 0, 1);
            panel.Controls.Add(_txtFfplay, 1, 1);
            var b2 = new Button { Text = "Examinar..." };
            b2.Click += (s, e) => Browse(_txtFfplay);
            panel.Controls.Add(b2, 2, 1);

            panel.Controls.Add(new Label { Text = "vlc:", AutoSize = true }, 0, 2);
            panel.Controls.Add(_txtVlc, 1, 2);
            var b3 = new Button { Text = "Examinar..." };
            b3.Click += (s, e) => Browse(_txtVlc);
            panel.Controls.Add(b3, 2, 2);

            var btnSave = new Button { Text = "Guardar", DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            flow.Controls.Add(btnSave);
            flow.Controls.Add(btnCancel);
            panel.Controls.Add(flow, 0, 3);
            panel.SetColumnSpan(flow, 3);

            Controls.Add(panel);

            // Load existing
            var cfg = Config.Load();
            _txtFfmpeg.Text = cfg.FfmpegPath ?? string.Empty;
            _txtFfplay.Text = cfg.FfplayPath ?? string.Empty;
            _txtVlc.Text = cfg.VlcPath ?? string.Empty;

            btnSave.Click += (s, e) => {
                var c = new Config { FfmpegPath = _txtFfmpeg.Text.Trim(), FfplayPath = _txtFfplay.Text.Trim(), VlcPath = _txtVlc.Text.Trim() };
                c.Save();
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        private void Browse(TextBox target)
        {
            using var dlg = new OpenFileDialog();
            dlg.Filter = "Executables|*.exe|All files|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                target.Text = dlg.FileName;
            }
        }
    }
}
