using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using VideoGateway.Testing.Common;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace VideoGateway.PublisherUI
{
    /// <summary>
    /// PublisherForm: displays available video files as a scrollable thumbnail grid.
    /// Selecting a card shows preview + metadata; the Publicar button streams the
    /// selected file via ffmpeg → RTSP. Uses TableLayoutPanel as the outer container
    /// to guarantee zero overlap between UI sections.
    /// </summary>
    public class PublisherForm : Form
    {
        // ── Palette ──────────────────────────────────────────────────────────────
        private static readonly Color BgColor      = Color.FromArgb(30,  30,  46);
        private static readonly Color CardColor    = Color.FromArgb(37,  37,  56);
        private static readonly Color TextColor    = Color.FromArgb(198, 208, 245);
        private static readonly Color DimTextColor = Color.FromArgb(165, 173, 206);
        private static readonly Color AccentBlue   = Color.FromArgb(140, 170, 238);
        private static readonly Color AccentGreen  = Color.FromArgb(166, 209, 137);
        private static readonly Color AccentRed    = Color.FromArgb(231, 130, 132);
        private static readonly Color AccentYellow = Color.FromArgb(229, 200, 144);

        // ── Toolbar controls ─────────────────────────────────────────────────────
        private readonly TextBox  _txtInputUrl = new() { Text = "", PlaceholderText = "O pega una URL de stream (ej: http://...m3u8)" };
        private readonly TextBox  _txtRtsp    = new() { Text = "rtsp://127.0.0.1:8554/live" };
        private readonly CheckBox _chkOriginalCodec = new() { Text = "Mantener códec original (no forzar H.264)", ForeColor = Color.FromArgb(229, 200, 144), AutoSize = true };
        private readonly Button   _btnPublish = new() { Text = "▶  Publicar"    };
        private readonly Button  _btnStop    = new() { Text = "■  Detener", Enabled = false };
        private readonly Button  _btnBrowse  = new() { Text = "📂  Examinar"   };
        private readonly Button  _btnConfig  = new() { Text = "⚙  Config"      };
        private readonly Label   _lblStatus  = new() { AutoSize = false         };

        // ── Detail panel controls ────────────────────────────────────────────────
        private readonly TextBox _txtMetadata = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly TextBox _txtLogs     = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly Label   _lblSelected = new() { AutoSize = false };
        private readonly Button  _btnPreview  = new() { Text = "▶  Preview" };
        private readonly Button  _btnStopPb   = new() { Text = "⏹  Stop"   };
        private readonly TrackBar _trkVolume  = new() { Minimum = 0, Maximum = 100, Value = 80, Width = 100 };

        // ── Grid ─────────────────────────────────────────────────────────────────
        private readonly FlowLayoutPanel _grid = new()
        {
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        private VideoCardPanel? _selectedCard;
        private readonly List<VideoCardPanel> _cards = new();

        // ── Embedded player ──────────────────────────────────────────────────────
        private VideoView?   _videoView;
        private LibVLC?      _libVLC;
        private MediaPlayer? _mediaPlayer;
        private bool         _vlcAvailable;
        private System.Diagnostics.Process? _publisherProcess;
        private readonly string _thumbCacheDir = Path.Combine(Path.GetTempPath(), "DdsVideoGW_thumbs");
        private readonly Label  _lblCount = new() { ForeColor = Color.FromArgb(165, 173, 206), Font = new Font("Segoe UI", 8.5F), AutoSize = true, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 8, 0) };

        // ═════════════════════════════════════════════════════════════════════════

        public PublisherForm()
        {
            Text        = "VideoGateway — Publisher";
            Width       = 1150;
            Height      = 760;
            MinimumSize = new Size(960, 620);
            BackColor   = BgColor;
            ForeColor   = TextColor;
            Font        = new Font("Segoe UI", 9F);

            Directory.CreateDirectory(_thumbCacheDir);

            // Outer layout: 3 rows → header | content | status
            var outer = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                RowCount    = 3,
                ColumnCount = 1,
                BackColor   = BgColor,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));   // header
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // content
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));   // status

            outer.Controls.Add(BuildHeader(),  0, 0);
            outer.Controls.Add(BuildContent(), 0, 1);
            outer.Controls.Add(BuildStatus(),  0, 2);

            Controls.Add(outer);

            WireEvents();

            var samples = ResolveSamplesFolder();
            if (Directory.Exists(samples)) LoadFolder(samples);
        }

        // ── Section builders ─────────────────────────────────────────────────────

        private Panel BuildHeader()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = CardColor, Padding = new Padding(14, 8, 14, 8) };

            var title = new Label
            {
                Text      = "📡  VideoGateway Publisher",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = AccentBlue,
                Dock      = DockStyle.Top,
                Height    = 26
            };

            var row = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 7,
                Padding     = new Padding(0, 2, 0, 0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));  // Input URL
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));  // Output URL
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13));  // Publicar
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11));  // Detener
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));  // Examinar
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9));   // Config
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 5));   // spacer

            var inputLbl = new Label { Text = "Origen (Archivo o URL):", ForeColor = DimTextColor, Dock = DockStyle.Top, AutoSize = true };
            StyleTextBox(_txtInputUrl); _txtInputUrl.Dock = DockStyle.Top;
            var inputBox = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 8, 0) };
            inputBox.Controls.Add(_txtInputUrl);
            inputBox.Controls.Add(inputLbl);

            var rtspLbl = new Label { Text = "URL RTSP Destino:", ForeColor = DimTextColor, Dock = DockStyle.Top, AutoSize = true };
            StyleTextBox(_txtRtsp); _txtRtsp.Dock = DockStyle.Top;
            _chkOriginalCodec.Dock = DockStyle.Bottom;
            
            var urlBox = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 8, 0) };
            urlBox.Controls.Add(_txtRtsp); 
            urlBox.Controls.Add(rtspLbl); 
            urlBox.Controls.Add(_chkOriginalCodec);

            StyleButton(_btnPublish, AccentGreen, BgColor); _btnPublish.Dock = DockStyle.Top; _btnPublish.Height = 36;
            StyleButton(_btnStop,    AccentRed,   BgColor); _btnStop.Dock    = DockStyle.Top; _btnStop.Height    = 36;
            StyleButton(_btnBrowse,  AccentBlue,  BgColor); _btnBrowse.Dock  = DockStyle.Top; _btnBrowse.Height  = 36;
            StyleButton(_btnConfig,  Color.FromArgb(116, 127, 162), BgColor); _btnConfig.Dock = DockStyle.Top; _btnConfig.Height = 36;

            row.Controls.Add(inputBox,    0, 0);
            row.Controls.Add(urlBox,      1, 0);
            row.Controls.Add(_btnPublish, 2, 0);
            row.Controls.Add(_btnStop,    3, 0);
            row.Controls.Add(_btnBrowse,  4, 0);
            row.Controls.Add(_btnConfig,  5, 0);

            p.Controls.Add(row);
            p.Controls.Add(title);
            return p;
        }

        private SplitContainer BuildContent()
        {
            // Main horizontal split: Grid (left 60%) | Detail (right 40%)
            var split = new SplitContainer
            {
                Dock             = DockStyle.Fill,
                Orientation      = Orientation.Vertical,
                SplitterWidth    = 6,
                BackColor        = BgColor
            };

            // We set splitter distance after handle is created to avoid layout issues
            split.HandleCreated += (_, _) =>
            {
                var dist = (int)(split.Width * 0.60);
                if (dist > split.Panel1MinSize && dist < split.Width - split.Panel2MinSize)
                    split.SplitterDistance = dist;
            };

            split.Panel1.BackColor = BgColor;
            split.Panel2.BackColor = BgColor;

            // Left: grid panel
            split.Panel1.Controls.Add(BuildGridPanel());

            // Right: detail panel
            split.Panel2.Controls.Add(BuildDetailPanel());

            return split;
        }

        private Panel BuildGridPanel()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = BgColor, Padding = new Padding(0) };

            // Grid header bar
            var header = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(24, 24, 38), Padding = new Padding(12, 0, 8, 0) };
            var lbl    = new Label  { Text = "  🎬  VÍDEOS DISPONIBLES", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = AccentBlue, Dock = DockStyle.Left, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
            var countLbl = new Label { Name = "lblCount", ForeColor = DimTextColor, Font = new Font("Segoe UI", 8.5F), Dock = DockStyle.Right, AutoSize = true, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 8, 0) };
            header.Controls.Add(countLbl);
            header.Controls.Add(lbl);

            // Scrollable flow grid
            _grid.Dock        = DockStyle.Fill;
            _grid.BackColor   = BgColor;
            _grid.Padding     = new Padding(10, 10, 10, 10);

            // Correct dock order: Top → Fill (last)
            p.Controls.Add(_grid);
            p.Controls.Add(header);
            return p;
        }

        private Panel BuildDetailPanel()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = BgColor, Padding = new Padding(0, 0, 0, 0) };

            // Selected file name banner
            _lblSelected.Dock      = DockStyle.Top;
            _lblSelected.Height    = 38;
            _lblSelected.BackColor = Color.FromArgb(24, 24, 38);
            _lblSelected.ForeColor = AccentYellow;
            _lblSelected.Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _lblSelected.Text      = "  Ningún vídeo seleccionado";
            _lblSelected.Padding   = new Padding(12, 0, 0, 0);
            _lblSelected.TextAlign = ContentAlignment.MiddleLeft;

            // Inner split: Video preview (top) | Tabs info+logs (bottom)
            var innerSplit = new SplitContainer
            {
                Dock          = DockStyle.Fill,
                Orientation   = Orientation.Horizontal,
                SplitterWidth = 5,
                BackColor     = BgColor
            };
            innerSplit.HandleCreated += (_, _) =>
            {
                var dist = (int)(innerSplit.Height * 0.45);
                if (dist > innerSplit.Panel1MinSize && dist < innerSplit.Height - innerSplit.Panel2MinSize)
                    innerSplit.SplitterDistance = dist;
            };

            innerSplit.Panel1.BackColor = Color.Black;
            innerSplit.Panel2.BackColor = CardColor;

            // Preview area
            BuildVideoPreviewPanel(innerSplit.Panel1);

            // Info + logs tabs
            BuildInfoLogsPanel(innerSplit.Panel2);

            // Correct order: Top → Fill
            p.Controls.Add(innerSplit);
            p.Controls.Add(_lblSelected);
            return p;
        }

        private void BuildVideoPreviewPanel(SplitterPanel panel)
        {
            // Playback controls (bottom of preview panel)
            var pbBar = new FlowLayoutPanel
            {
                Dock          = DockStyle.Bottom,
                Height        = 36,
                BackColor     = CardColor,
                Padding       = new Padding(8, 4, 8, 4),
                FlowDirection = FlowDirection.LeftToRight
            };
            StyleButton(_btnPreview, AccentGreen, BgColor); _btnPreview.Width = 90; _btnPreview.Height = 26;
            StyleButton(_btnStopPb,  AccentRed,   BgColor); _btnStopPb.Width  = 60; _btnStopPb.Height  = 26;
            var lblVol = new Label { Text = "Vol:", ForeColor = DimTextColor, AutoSize = true, Padding = new Padding(6, 4, 0, 0) };
            StyleTrackBar(_trkVolume); _trkVolume.Height = 22;
            pbBar.Controls.AddRange(new Control[] { _btnPreview, _btnStopPb, lblVol, _trkVolume });

            // Video view
            try
            {
                _libVLC      = new LibVLC();
                _mediaPlayer = new MediaPlayer(_libVLC);
                _videoView   = new VideoView { MediaPlayer = _mediaPlayer, Dock = DockStyle.Fill };
                _vlcAvailable = true;
                panel.Controls.Add(_videoView);
            }
            catch
            {
                _vlcAvailable = false;
                var lbl = new Label
                {
                    Text      = "Reproductor embebido no disponible.\n(LibVLC nativo no cargado)\n\nUsa el botón ▶ Preview para abrir con ffplay.",
                    ForeColor = AccentRed,
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font      = new Font("Segoe UI", 10F, FontStyle.Bold)
                };
                panel.Controls.Add(lbl);
            }
            panel.Controls.Add(pbBar);
        }

        private void BuildInfoLogsPanel(SplitterPanel panel)
        {
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            var pageInfo = new TabPage("ℹ  Info (ffprobe)") { BackColor = CardColor, Padding = new Padding(4) };
            StyleTextBox(_txtMetadata); _txtMetadata.Dock = DockStyle.Fill;
            pageInfo.Controls.Add(_txtMetadata);

            var pageLogs = new TabPage("📋  Logs FFmpeg") { BackColor = CardColor, Padding = new Padding(4) };
            StyleTextBox(_txtLogs); _txtLogs.Dock = DockStyle.Fill;
            pageLogs.Controls.Add(_txtLogs);

            tabs.TabPages.Add(pageInfo);
            tabs.TabPages.Add(pageLogs);
            panel.Controls.Add(tabs);
        }

        private Panel BuildStatus()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 20, 32) };
            StyleLabel(_lblStatus);
            _lblStatus.Dock    = DockStyle.Fill;
            _lblStatus.Padding = new Padding(10, 4, 0, 0);
            _lblStatus.Text    = "Estado: Inactivo.";
            p.Controls.Add(_lblStatus);
            return p;
        }

        // ── Grid population ───────────────────────────────────────────────────────

        private void LoadFolder(string folder)
        {
            // Clear existing cards
            foreach (var c in _cards) c.Dispose();
            _cards.Clear();
            _grid.Controls.Clear();
            _selectedCard = null;
            UpdateSelectedLabel(null);

            var exts = new[] { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".ts", ".flv", ".jpg", ".jpeg" };
            var files = Directory.EnumerateFiles(folder)
                .Where(f => exts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f)
                .ToArray();

            // Update count label
            var countLbl = _grid.Parent?.Controls.OfType<Panel>()
                .SelectMany(p => p.Controls.OfType<Label>())
                .FirstOrDefault(l => l.Name == "lblCount");
            if (countLbl != null) countLbl.Text = $"{files.Length} archivo(s)  ";

            if (files.Length == 0)
            {
                var empty = new Label
                {
                    Text      = "No se encontraron archivos de vídeo en esta carpeta.\nUsa \"📂 Examinar\" para seleccionar otra.",
                    ForeColor = DimTextColor,
                    Font      = new Font("Segoe UI", 11F),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock      = DockStyle.Fill
                };
                _grid.Controls.Add(empty);
                return;
            }

            foreach (var file in files)
            {
                var card = new VideoCardPanel(file, Path.GetExtension(file).TrimStart('.').ToUpper());
                card.CardClicked += OnCardClicked;
                _cards.Add(card);
                _grid.Controls.Add(card);
            }

            // Generate thumbnails asynchronously
            Task.Run(() => GenerateThumbnailsAsync(files));
        }

        private void GenerateThumbnailsAsync(string[] files)
        {
            foreach (var file in files)
            {
                using var md5 = MD5.Create();
                var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(file));
                var hashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                var thumbPath = Path.Combine(_thumbCacheDir, hashStr + ".jpg");

                if (!File.Exists(thumbPath))
                {
                    try
                    {
                        var args = $"-y -hide_banner -loglevel quiet -i \"{file}\" -ss 00:00:02 -vframes 1 " +
                                   $"-vf \"scale=198:112:force_original_aspect_ratio=decrease,pad=198:112:(ow-iw)/2:(oh-ih)/2:color=#1e1e2e\" " +
                                   $"-q:v 4 \"{thumbPath}\"";
                        var p = ProcessRunner.StartProcess("ffmpeg", args, _ => { });
                        try { p?.WaitForExit(8000); } catch { }
                    }
                    catch { /* thumbnail generation is best-effort */ }
                }

                if (File.Exists(thumbPath))
                {
                    try
                    {
                        // Load into memory to avoid file locks
                        Bitmap bmp;
                        using (var fs = new FileStream(thumbPath, FileMode.Open, FileAccess.Read))
                            bmp = new Bitmap(fs);

                        var captured = file;
                        try
                        {
                            BeginInvoke(() =>
                            {
                                var card = _cards.FirstOrDefault(c => c.FilePath == captured);
                                card?.SetThumbnail(bmp);
                            });
                        }
                        catch { bmp.Dispose(); }
                    }
                    catch { }
                }
            }
        }

        private void OnCardClicked(object? sender, EventArgs e)
        {
            if (sender is not VideoCardPanel card) return;

            // Deselect previous
            if (_selectedCard != null && _selectedCard != card)
                _selectedCard.IsSelected = false;

            _selectedCard = card;
            card.IsSelected = true;

            UpdateSelectedLabel(card.FilePath);
            _txtInputUrl.Text = string.Empty; // Clear URL when selecting a file
            LoadMetadataAsync(card.FilePath);
        }

        private void UpdateSelectedLabel(string? path)
        {
            if (path == null)
            {
                _lblSelected.Text      = "  Ningún vídeo seleccionado";
                _lblSelected.ForeColor = DimTextColor;
            }
            else
            {
                _lblSelected.Text      = $"  🎬  {Path.GetFileName(path)}";
                _lblSelected.ForeColor = AccentYellow;
            }
        }

        private void LoadMetadataAsync(string file)
        {
            _txtMetadata.Text = "Cargando metadatos con ffprobe…";
            Task.Run(() =>
            {
                var meta = MediaInfo.GetMetadataWithFfprobe(file);
                try { BeginInvoke(() => _txtMetadata.Text = meta); } catch { }
            });
        }

        // ── Events ───────────────────────────────────────────────────────────────

        private void WireEvents()
        {
            _btnBrowse.Click  += (_, _) => { using var d = new FolderBrowserDialog(); if (d.ShowDialog() == DialogResult.OK) LoadFolder(d.SelectedPath); };
            _btnPublish.Click += BtnPublish_Click;
            _btnStop.Click    += BtnStop_Click;
            _btnConfig.Click  += (_, _) => { using var d = new SettingsForm(); d.ShowDialog(this); };
            _btnPreview.Click += (_, _) =>
            {
                var url = _txtInputUrl.Text?.Trim();
                if (!string.IsNullOrEmpty(url))
                {
                    PlayFile(url);
                    return;
                }

                if (_selectedCard != null)
                {
                    PlayFile(_selectedCard.FilePath);
                    return;
                }

                MessageBox.Show("Selecciona un vídeo o pega una URL de stream para previsualizar.", "Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            _btnStopPb.Click  += (_, _) => { try { _mediaPlayer?.Stop(); } catch { } SetStatus("Reproducción detenida.", TextColor); };
            _trkVolume.Scroll += (_, _) => { try { if (_mediaPlayer != null) _mediaPlayer.Volume = _trkVolume.Value; } catch { } };
        }

        private void BtnPublish_Click(object? sender, EventArgs e)
        {
            string file;
            bool isNetwork = !string.IsNullOrWhiteSpace(_txtInputUrl.Text);
            
            if (isNetwork)
            {
                file = _txtInputUrl.Text.Trim();
            }
            else
            {
                if (_selectedCard == null) { MessageBox.Show("Selecciona un vídeo o pega una URL de stream.", "Sin origen", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                file = _selectedCard.FilePath;
            }
            var rtsp = _txtRtsp.Text.Trim();
            if (string.IsNullOrEmpty(rtsp)) { MessageBox.Show("Indica la URL RTSP destino."); return; }
            var codecArgs = _chkOriginalCodec.Checked
                ? "-c copy"
                : "-c:v libx264 -preset ultrafast -tune zerolatency -c:a aac";

            // Detect URL scheme to set ffmpeg RTSP transport for input if needed
            string inputTransportPrefix = string.Empty;
            try
            {
                if (Uri.TryCreate(file, UriKind.Absolute, out var uri) && uri.Scheme.Equals("rtsp", StringComparison.OrdinalIgnoreCase))
                {
                    // User requirement: prefer UDP for RTSP
                    inputTransportPrefix = "-rtsp_transport udp ";
                }
            }
            catch { }

            // Build ffmpeg args: place transport option before -i when reading RTSP inputs
            var args = $"-re {inputTransportPrefix}-i \"{file}\" {codecArgs} -f rtsp \"{rtsp}\"";
            _txtLogs.Clear();
            AppendLog($"Publicando origen: {(isNetwork ? "Stream de Red" : Path.GetFileName(file))}");
            AppendLog($"Destino:    {rtsp}");
            AppendLog($"Comando:    ffmpeg {args}");
            AppendLog(new string('─', 60));

            // Send the input URL to any running Subscriber via UDP so it can auto-connect.
            try
            {
                var subscriberHost = Environment.GetEnvironmentVariable("VIDEOGATEWAY_SUBSCRIBER_HOST") ?? "127.0.0.1";
                var subscriberPortStr = Environment.GetEnvironmentVariable("VIDEOGATEWAY_SUBSCRIBER_PORT");
                var subscriberPort = 50000;
                if (!string.IsNullOrEmpty(subscriberPortStr) && int.TryParse(subscriberPortStr, out var p)) subscriberPort = p;
                using var udp = new UdpClient();
                var msg = Encoding.UTF8.GetBytes(file);
                udp.Send(msg, msg.Length, subscriberHost, subscriberPort);
                AppendLog($"UDP: sent URL to {subscriberHost}:{subscriberPort}");
            }
            catch (Exception ex)
            {
                AppendLog("UDP send failed: " + ex.Message);
            }

            // Switch to logs tab
            var tabs = _txtLogs.Parent?.Parent as TabControl;
            if (tabs != null) tabs.SelectedIndex = 1;

            // Ensure we show a preview in the embedded player before publishing
            try
            {
                if (!string.IsNullOrWhiteSpace(file)) PlayFile(file);
            }
            catch (Exception ex)
            {
                AppendLog("Preview error: " + ex.Message);
            }

            _publisherProcess = ProcessRunner.StartProcess("ffmpeg", args, line =>
            {
                try { BeginInvoke(() => _txtLogs.AppendText(line + Environment.NewLine)); } catch { }
            });

            if (_publisherProcess == null) { SetStatus("Error: ffmpeg no encontrado en PATH.", AccentRed); return; }

            _btnPublish.Enabled = false;
            _btnStop.Enabled    = true;
            SetStatus($"▶  Publicando '{(isNetwork ? "Red" : Path.GetFileName(file))}'  →  {rtsp}  (PID {_publisherProcess.Id})", AccentGreen);
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            try { if (_publisherProcess is { HasExited: false }) _publisherProcess.Kill(); } catch { }
            try { _publisherProcess?.Dispose(); } catch { }
            _publisherProcess   = null;
            _btnPublish.Enabled = true;
            _btnStop.Enabled    = false;
            AppendLog("── Publicación detenida ──");
            SetStatus("■  Publicación detenida.", TextColor);
        }

        private void PlayFile(string file)
        {
            if (_vlcAvailable && _mediaPlayer != null && _libVLC != null)
            {
                try
                {
                    var media = new Media(_libVLC, file, FromType.FromLocation);
                    // For RTSP streams prefer UDP transport (user requirement)
                    if (file.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
                    {
                        media.AddOption(":rtsp-transport=udp");
                        media.AddOption(":network-caching=3000");
                    }
                    _mediaPlayer.Play(media);
                    _mediaPlayer.Volume = _trkVolume.Value;
                    SetStatus($"▶  Preview: {Path.GetFileName(file)}", AccentBlue);
                    return;
                }
                catch { }
            }
            var ffplay = ProcessRunner.TryFindExecutable("ffplay");
            var proc   = ProcessRunner.StartProcess(ffplay ?? "ffplay", $"-autoexit \"{file}\"", _ => { });
            if (proc != null) SetStatus($"ffplay: {Path.GetFileName(file)} (PID {proc.Id})", AccentBlue);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void AppendLog(string msg)
        {
            _txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}]  {msg}{Environment.NewLine}");
        }

        private void SetStatus(string text, Color color)
        {
            _lblStatus.Text      = text;
            _lblStatus.ForeColor = color;
        }

        private static string ResolveSamplesFolder()
            => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Samples"));

        // ── Styling ───────────────────────────────────────────────────────────────

        private static void StyleButton(Button b, Color back, Color fore)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = back; b.ForeColor = fore;
            b.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            b.Cursor = Cursors.Hand; b.Height = 30;
            b.Margin = new Padding(3, 2, 3, 2);
        }

        private static void StyleTextBox(TextBox t)
        {
            t.BackColor = Color.FromArgb(17, 17, 27);
            t.ForeColor = Color.FromArgb(198, 208, 245);
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = new Font("Consolas", 9F);
        }

        private static void StyleLabel(Label l)
        {
            l.ForeColor = Color.FromArgb(198, 208, 245);
            l.Font = new Font("Segoe UI", 9F);
        }

        private static void StyleTrackBar(TrackBar t) => t.BackColor = Color.FromArgb(37, 37, 56);

        // ── Cleanup ───────────────────────────────────────────────────────────────

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            BtnStop_Click(this, EventArgs.Empty);
            try { _mediaPlayer?.Stop(); _mediaPlayer?.Dispose(); _libVLC?.Dispose(); _videoView?.Dispose(); } catch { }
        }

        // ═════════════════════════════════════════════════════════════════════════
        // ── Inner class: VideoCardPanel ──────────────────────────────────────────
        // ═════════════════════════════════════════════════════════════════════════

        private sealed class VideoCardPanel : Panel
        {
            public string FilePath { get; }
            public event EventHandler? CardClicked;

            private static readonly Color CNormal   = Color.FromArgb(37,  37,  56);
            private static readonly Color CHover    = Color.FromArgb(49,  50,  68);
            private static readonly Color CSelected = Color.FromArgb(42,  56,  88);
            private static readonly Color CText     = Color.FromArgb(198, 208, 245);
            private static readonly Color CDim      = Color.FromArgb(165, 173, 206);
            private static readonly Color CBlue     = Color.FromArgb(140, 170, 238);
            private static readonly Color CBorder   = Color.FromArgb(140, 170, 238);

            private readonly PictureBox _thumb;
            private readonly Label      _lblName;
            private readonly Label      _lblExt;
            private bool                _selected;

            public bool IsSelected
            {
                get => _selected;
                set
                {
                    _selected    = value;
                    BackColor    = value ? CSelected : CNormal;
                    Invalidate(); // triggers OnPaint for border
                }
            }

            public VideoCardPanel(string filePath, string ext)
            {
                FilePath  = filePath;
                Size      = new Size(210, 162);
                BackColor = CNormal;
                Cursor    = Cursors.Hand;
                Margin    = new Padding(8);
                Padding   = new Padding(3);

                // Thumbnail
                _thumb = new PictureBox
                {
                    Location  = new Point(3, 3),
                    Size      = new Size(204, 115),
                    BackColor = Color.FromArgb(17, 17, 27),
                    SizeMode  = PictureBoxSizeMode.Zoom,
                    Image     = BuildPlaceholder(204, 115)
                };

                // File name
                _lblName = new Label
                {
                    Location    = new Point(3, 121),
                    Size        = new Size(204, 22),
                    Text        = Path.GetFileName(filePath),
                    Font        = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    ForeColor   = CText,
                    AutoEllipsis = true
                };

                // Extension badge
                _lblExt = new Label
                {
                    Location  = new Point(3, 141),
                    Size      = new Size(204, 18),
                    Text      = $"  {ext}",
                    Font      = new Font("Segoe UI", 8F),
                    ForeColor = CBlue
                };

                Controls.AddRange(new Control[] { _thumb, _lblName, _lblExt });

                // Events
                void Click(object? s, EventArgs e) => CardClicked?.Invoke(this, e);
                this.Click   += Click;
                _thumb.Click += Click;
                _lblName.Click += Click;
                _lblExt.Click  += Click;

                void Enter(object? s, EventArgs e) { if (!_selected) BackColor = CHover; }
                void Leave(object? s, EventArgs e) { if (!_selected) BackColor = CNormal; }
                MouseEnter += Enter; _thumb.MouseEnter += Enter; _lblName.MouseEnter += Enter;
                MouseLeave += Leave; _thumb.MouseLeave += Leave; _lblName.MouseLeave += Leave;
            }

            public void SetThumbnail(Bitmap bmp)
            {
                var old = _thumb.Image;
                _thumb.Image = bmp;
                if (!ReferenceEquals(old, bmp)) old?.Dispose();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (_selected)
                {
                    using var pen = new Pen(CBorder, 2);
                    e.Graphics.DrawRectangle(pen, 1, 1, Width - 2, Height - 2);
                }
            }

            private static Bitmap BuildPlaceholder(int w, int h)
            {
                var bmp = new Bitmap(w, h);
                using var g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(17, 17, 27));
                // Film frame icon outline
                using var pen = new Pen(Color.FromArgb(49, 50, 68), 1);
                var cx = w / 2; var cy = h / 2;
                g.DrawRectangle(pen, cx - 28, cy - 20, 56, 40);
                // Play triangle
                var pts = new PointF[] { new(cx - 10, cy - 13), new(cx + 14, cy), new(cx - 10, cy + 13) };
                using var brush = new SolidBrush(Color.FromArgb(65, 68, 95));
                g.FillPolygon(brush, pts);
                return bmp;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try { _thumb.Image?.Dispose(); } catch { }
                }
                base.Dispose(disposing);
            }
        }
    }
}
