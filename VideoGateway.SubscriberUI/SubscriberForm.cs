using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VideoGateway.Testing.Common;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace VideoGateway.SubscriberUI
{
    /// <summary>
    /// SubscriberForm: connects to an RTSP URL and plays the stream in an embedded
    /// LibVLC video view with a real-time log panel below. Layout uses a SplitContainer
    /// so the video and log areas never overlap regardless of window size.
    /// </summary>
    public class SubscriberForm : Form
    {
        // ── Palette (Catppuccin Mocha) ─────────────────────────────────────────────
        private static readonly Color BgColor      = Color.FromArgb(30,  30,  46);
        private static readonly Color CardColor    = Color.FromArgb(37,  37,  56);
        private static readonly Color InputColor   = Color.FromArgb(17,  17,  27);
        private static readonly Color TextColor    = Color.FromArgb(198, 208, 245);
        private static readonly Color DimTextColor = Color.FromArgb(165, 173, 206);
        private static readonly Color AccentBlue   = Color.FromArgb(140, 170, 238);
        private static readonly Color AccentGreen  = Color.FromArgb(166, 209, 137);
        private static readonly Color AccentRed    = Color.FromArgb(231, 130, 132);
        private static readonly Color AccentYellow = Color.FromArgb(229, 200, 144);

        // ── UI Controls ───────────────────────────────────────────────────────────
        private readonly TextBox  _txtRtsp   = new() { Text = "rtsp://127.0.0.1:8554/live" };
        private readonly Button   _btnPlay   = new() { Text = "▶ Conectar"   };
        private readonly Button   _btnStop   = new() { Text = "⏹ Detener", Enabled = false };
        private readonly Button   _btnVlc    = new() { Text = "🔗 VLC ext."  };
        private readonly Button   _btnConfig = new() { Text = "⚙ Config"     };
        private readonly TrackBar _trkVolume = new() { Minimum = 0, Maximum = 100, Value = 80, Width = 110 };
        private readonly Label    _lblStatus = new() { AutoSize = false       };
        private readonly Label    _lblInfo   = new() { AutoSize = false       };
        private readonly TextBox  _txtLog    = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

        // Embedded player
        private VideoView?   _videoView;
        private LibVLC?      _libVLC;
        private MediaPlayer? _mediaPlayer;
        private bool         _vlcAvailable;
        // UDP listener for dynamic URL reception
        private CancellationTokenSource? _udpListenerCts;
        private const int UdpListenerPort = 50000;

        // ─────────────────────────────────────────────────────────────────────────
        public SubscriberForm()
        {
            Text        = "VideoGateway — Subscriber";
            Width       = 920;
            Height      = 660;
            MinimumSize = new Size(720, 520);
            BackColor   = BgColor;
            ForeColor   = TextColor;
            Font        = new Font("Segoe UI", 9F);

            BuildLayout();
            WireEvents();
            StartUdpUrlListener();
            // Initialize LibVLC asynchronously to avoid startup hangs
            Task.Run(() => TryInitializeLibVLCAsync());
            AppendLog("Subscriber listo. Introduce una URL RTSP y pulsa Conectar.");
            AppendLog($"Formato recomendado: H.264 / AAC — compatible con MediaMTX, VLC y FFplay.");
        }

        private void TryInitializeLibVLCAsync()
        {
            try
            {
                // Attempt to create LibVLC in background; if it succeeds, create UI controls on UI thread
                var lib = new LibVLC(new[] { "--verbose=2" });
                lib.Log += (s, e) => AppendLog($"VLC: {e.Message}");
                var mediaPlayer = new MediaPlayer(lib);
                BeginInvoke(() =>
                {
                    try
                    {
                        _libVLC = lib;
                        _mediaPlayer = mediaPlayer;
                        // Remove existing placeholder and add VideoView
                        if (_videoView != null) { }
                        var parent = Controls.OfType<SplitContainer>().FirstOrDefault()?.Panel1;
                        if (parent != null)
                        {
                            parent.Controls.Clear();
                            _videoView = new VideoView { MediaPlayer = _mediaPlayer, Dock = DockStyle.Fill };
                            parent.Controls.Add(_videoView);
                        }
                        _vlcAvailable = true;
                        AppendLog("LibVLC inicializado correctamente.");
                    }
                    catch (Exception ex)
                    {
                        AppendLog("Error inicializando UI LibVLC: " + ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                AppendLog("LibVLC native init failed: " + ex.Message);
            }
        }

        private void StartUdpUrlListener()
        {
            try
            {
                _udpListenerCts = new CancellationTokenSource();
                var ct = _udpListenerCts.Token;
                Task.Run(async () =>
                {
                    using var client = new UdpClient(UdpListenerPort);
                    AppendLog($"UDP URL listener started on port {UdpListenerPort} (send a plain URL or JSON {{\"url\":\"rtsp://...\"}}).");
                    while (!ct.IsCancellationRequested)
                    {
                        try
                        {
                            var res = await client.ReceiveAsync(ct);
                            var text = string.Empty;
                            try { text = Encoding.UTF8.GetString(res.Buffer).Trim(); } catch { }
                            if (string.IsNullOrWhiteSpace(text)) continue;

                            // Accept either raw URL or JSON with a url field
                            string? url = null;
                            if (text.StartsWith("{"))
                            {
                                try
                                {
                                    // crude JSON parse to avoid adding dependencies
                                    var idx = text.IndexOf("\"url\"", StringComparison.OrdinalIgnoreCase);
                                    if (idx >= 0)
                                    {
                                        var colon = text.IndexOf(':', idx);
                                        if (colon >= 0)
                                        {
                                            var start = text.IndexOf('"', colon + 1);
                                            var end = text.IndexOf('"', start + 1);
                                            if (start >= 0 && end > start) url = text.Substring(start + 1, end - start - 1);
                                        }
                                    }
                                }
                                catch { }
                            }
                            else
                            {
                                url = text;
                            }

                            if (!string.IsNullOrWhiteSpace(url))
                            {
                                BeginInvoke(() =>
                                {
                                    AppendLog($"UDP: received URL -> {url}");
                                    _txtRtsp.Text = url;
                                    // Auto-connect: simulate play click
                                    try { BtnPlay_Click(this, EventArgs.Empty); } catch { }
                                });
                            }
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex) { AppendLog("UDP listener error: " + ex.Message); await Task.Delay(500, ct); }
                    }
                }, ct);
            }
            catch (Exception ex) { AppendLog("Unable to start UDP listener: " + ex.Message); }
        }

        // ── Layout ───────────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            // ── Status bar (outermost bottom) ─────────────────────────────────
            StyleLabel(_lblStatus);
            _lblStatus.Dock      = DockStyle.Bottom;
            _lblStatus.Height    = 22;
            _lblStatus.Text      = "  Estado: Inactivo.";
            _lblStatus.BackColor = Color.FromArgb(24, 24, 38);
            _lblStatus.Padding   = new Padding(6, 4, 0, 0);

            // ── Info bar (second bottom, above status) ────────────────────────
            StyleLabel(_lblInfo);
            _lblInfo.Dock      = DockStyle.Bottom;
            _lblInfo.Height    = 22;
            _lblInfo.Text      = "  Requisito: H.264 + AAC — Compatible con VLC, FFplay y MediaMTX (puerto 8554).";
            _lblInfo.ForeColor = DimTextColor;
            _lblInfo.BackColor = CardColor;
            _lblInfo.Padding   = new Padding(6, 4, 0, 0);

            // ── Header: RTSP URL + control buttons (Top) ──────────────────────
            var headerPanel = BuildHeader();

            // ── SplitContainer: video (top) | logs (bottom) ───────────────────
            var split = new SplitContainer
            {
                Dock             = DockStyle.Fill,
                Orientation      = Orientation.Horizontal,
                SplitterDistance = 360,
                Panel1MinSize    = 120,
                Panel2MinSize    = 80,
                BackColor        = BgColor,
                SplitterWidth    = 5
            };
            split.Panel1.BackColor = Color.Black;
            split.Panel2.BackColor = CardColor;

            // Video panel (Panel1)
            BuildVideoPanel(split.Panel1);

            // Log panel (Panel2)
            BuildLogPanel(split.Panel2);

            // ── Assemble form: Top → Bottoms → Fill (LAST) ────────────────────
            Controls.Add(headerPanel);  // Top    (first → outermost top)
            Controls.Add(_lblStatus);   // Bottom (second → outermost bottom)
            Controls.Add(_lblInfo);     // Bottom (third → just above status)
            Controls.Add(split);        // Fill   (last → remaining space)
        }

        private Panel BuildHeader()
        {
            var header = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 98,
                BackColor = CardColor,
                Padding   = new Padding(10, 8, 10, 6)
            };

            // Title row
            var lblTitle = new Label
            {
                Text      = "📡  VideoGateway Subscriber",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = AccentBlue,
                Dock      = DockStyle.Top,
                Height    = 26
            };

            // Controls row
            var ctrlRow = new TableLayoutPanel
            {
                Dock        = DockStyle.Bottom,
                Height      = 58,
                ColumnCount = 6,
                Padding     = new Padding(0)
            };
            ctrlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42)); // URL
            ctrlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14)); // Conectar
            ctrlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14)); // Detener
            ctrlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14)); // VLC ext
            ctrlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48)); // Vol label
            ctrlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16)); // Volume slider + Config

            // RTSP URL textbox with label
            var rtspLabel = new Label { Text = "URL Servidor RTSP:", ForeColor = DimTextColor, Dock = DockStyle.Top, AutoSize = true };
            StyleTextBox(_txtRtsp); _txtRtsp.Dock = DockStyle.Top;
            var rtspBox = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 6, 0) };
            rtspBox.Controls.Add(_txtRtsp);
            rtspBox.Controls.Add(rtspLabel);

            StyleButton(_btnPlay,   AccentGreen,                        BgColor); _btnPlay.Dock   = DockStyle.Top; _btnPlay.Height   = 34;
            StyleButton(_btnStop,   AccentRed,                          BgColor); _btnStop.Dock   = DockStyle.Top; _btnStop.Height   = 34;
            StyleButton(_btnVlc,    AccentBlue,                         BgColor); _btnVlc.Dock    = DockStyle.Top; _btnVlc.Height    = 34;
            StyleButton(_btnConfig, Color.FromArgb(116, 127, 162),      BgColor); _btnConfig.Dock = DockStyle.Top; _btnConfig.Height = 34;

            var lblVol = new Label { Text = "Vol:", ForeColor = DimTextColor, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(0, 0, 0, 8) };
            StyleTrackBar(_trkVolume); _trkVolume.Dock = DockStyle.Top; _trkVolume.Height = 24;

            // volume + config in a stacked panel
            var volBox = new Panel { Dock = DockStyle.Fill };
            var lblVolTop = new Label { Text = "Volumen", ForeColor = DimTextColor, Dock = DockStyle.Top, Height = 16, Font = new Font("Segoe UI", 7.5F) };
            volBox.Controls.Add(_trkVolume);
            volBox.Controls.Add(lblVolTop);

            ctrlRow.Controls.Add(rtspBox,   0, 0);
            ctrlRow.Controls.Add(_btnPlay,  1, 0);
            ctrlRow.Controls.Add(_btnStop,  2, 0);
            ctrlRow.Controls.Add(_btnVlc,   3, 0);
            ctrlRow.Controls.Add(lblVol,    4, 0);
            ctrlRow.Controls.Add(volBox,    5, 0);

            header.Controls.Add(ctrlRow);
            header.Controls.Add(lblTitle);
            return header;
        }

        private void BuildVideoPanel(SplitterPanel panel)
        {
            // At startup we do not block trying to load native LibVLC synchronously
            // Create a placeholder label; LibVLC will be initialized asynchronously
            _vlcAvailable = false;
            var placeholder = new Label
            {
                Text      = "Cargando reproductor embebido...",
                ForeColor = DimTextColor,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 10.5F, FontStyle.Regular)
            };
            panel.Controls.Add(placeholder);
        }

        private void BuildLogPanel(SplitterPanel panel)
        {
            var logHeader = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Color.FromArgb(24, 24, 38) };
            var lblLog = new Label
            {
                Text      = "  📋 Log de Conexión",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = AccentYellow,
                Dock      = DockStyle.Fill
            };
            var btnClear = new Button { Text = "Limpiar", Width = 70, Height = 22, Dock = DockStyle.Right };
            StyleButton(btnClear, Color.FromArgb(49, 50, 68), TextColor); btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (_, _) => _txtLog.Clear();
            logHeader.Controls.Add(lblLog);
            logHeader.Controls.Add(btnClear);

            StyleTextBox(_txtLog);
            _txtLog.Dock      = DockStyle.Fill;
            _txtLog.BackColor = Color.FromArgb(22, 22, 35);

            // Add in correct dock order
            panel.Controls.Add(_txtLog);    // Fill (last)
            panel.Controls.Add(logHeader);  // Top (first)
        }

        // ── Events ───────────────────────────────────────────────────────────────

        private void WireEvents()
        {
            _btnPlay.Click   += BtnPlay_Click;
            _btnStop.Click   += BtnStop_Click;
            _btnVlc.Click    += BtnVlc_Click;
            _btnConfig.Click += BtnConfig_Click;
            _trkVolume.Scroll += (_, _) => { try { if (_mediaPlayer != null) _mediaPlayer.Volume = _trkVolume.Value; } catch { } };
        }

        private void BtnPlay_Click(object? sender, EventArgs e)
        {
            var url = _txtRtsp.Text.Trim();
            if (string.IsNullOrEmpty(url)) { MessageBox.Show("Introduce una URL RTSP."); return; }

            AppendLog($"Conectando a: {url} …");

            // Detect input format with ffprobe (if available) and log it to help debugging
            try
            {
                var fmt = VideoGateway.Testing.Common.MediaInfo.DetectFormatWithFfprobe(url);
                AppendLog($"Formato detectado (entrada): {fmt}");
            }
            catch { }

            if (_vlcAvailable && _mediaPlayer != null && _libVLC != null)
            {
                try
                {
                    var media = new Media(_libVLC, url, FromType.FromLocation);
                    // Try to detect resolution via ffprobe and force aspect ratio so the full image is visible (letterbox)
                    try
                    {
                        var res = VideoGateway.Testing.Common.MediaInfo.DetectVideoResolutionWithFfprobe(url);
                        if (res.HasValue)
                        {
                            media.AddOption($":aspect-ratio={res.Value.width}:{res.Value.height}");
                            AppendLog($"Forzando aspect-ratio {res.Value.width}:{res.Value.height} para mostrar imagen completa.");
                        }
                    }
                    catch { }
                    // Prefer UDP transport and increase cache for unstable networks
                    media.AddOption(":rtsp-transport=udp");
                    media.AddOption(":network-caching=3000");
                    media.AddOption(":no-video-title-show");

                    // Parse media metadata asynchronously to avoid blocking the UI thread.
                    var parsed = false;
                    media.ParsedChanged += (_, __) =>
                    {
                        try
                        {
                            parsed = true;
                            AppendLog("Media parsed (network). See ffprobe for detailed streams.");
                        }
                        catch { }
                    };

                    // Use background parsing to avoid blocking the UI thread
                    Task.Run(() =>
                    {
                        try { media.Parse(MediaParseOptions.ParseNetwork); } catch { }
                    });
                    Task.Run(async () =>
                    {
                        await Task.Delay(5000);
                        if (!parsed) AppendLog("Warning: media parse timeout (still waiting for metadata).");
                    });

                    _mediaPlayer.Stop();
                    _mediaPlayer.Play(media);
                    _mediaPlayer.Volume = _trkVolume.Value;

                    // Hook events for logging
                    _mediaPlayer.Playing  -= OnPlaying;
                    _mediaPlayer.Stopped  -= OnStopped;
                    _mediaPlayer.EncounteredError -= OnError;
                    _mediaPlayer.Playing  += OnPlaying;
                    _mediaPlayer.Stopped  += OnStopped;
                    _mediaPlayer.EncounteredError += OnError;

                    _btnPlay.Enabled = false;
                    _btnStop.Enabled = true;
                    SetStatus("Conectando… (esperando primer frame)", AccentYellow);
                    return;
                }
                catch (Exception ex)
                {
                    AppendLog($"[ERROR] No se pudo iniciar reproductor embebido: {ex.Message}");
                }
            }

            // Fallback: ffplay
            AppendLog("LibVLC no disponible, intentando ffplay…");
            var p = ProcessRunner.StartProcess("ffplay", $"-autoexit \"{url}\"", _ => { });
            if (p == null)
            {
                AppendLog("[ERROR] No se pudo iniciar ffplay. ¿Está en PATH?");
                SetStatus("Error al conectar.", AccentRed);
            }
            else
            {
                AppendLog($"ffplay iniciado con PID {p.Id}.");
                SetStatus($"ffplay iniciado (PID {p.Id}).", AccentBlue);
            }
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            try { _mediaPlayer?.Stop(); } catch { }
            _btnPlay.Enabled = true;
            _btnStop.Enabled = false;
            SetStatus("Reproducción detenida.", TextColor);
            AppendLog("── Reproducción detenida por el usuario ──");
        }

        private void BtnVlc_Click(object? sender, EventArgs e)
        {
            var url = _txtRtsp.Text.Trim();
            if (string.IsNullOrEmpty(url)) { MessageBox.Show("Introduce una URL RTSP."); return; }

            AppendLog($"Abriendo URL en VLC externo: {url}");

            // Try VLC by path, then by name, then ffplay, then default app
            var vlcPath = ProcessRunner.TryFindExecutable("vlc");
            var launcher = !string.IsNullOrEmpty(vlcPath) ? vlcPath : "vlc";
            var pVlc = ProcessRunner.StartProcess(launcher, $"\"{url}\"", _ => { });
            if (pVlc != null) { AppendLog($"VLC externo lanzado (PID {pVlc.Id})."); SetStatus($"VLC ext. (PID {pVlc.Id}).", AccentBlue); return; }

            var ffplay = ProcessRunner.TryFindExecutable("ffplay");
            if (!string.IsNullOrEmpty(ffplay))
            {
                var pFf = ProcessRunner.StartProcess(ffplay, $"-autoexit \"{url}\"", _ => { });
                if (pFf != null) { AppendLog($"ffplay lanzado (PID {pFf.Id})."); SetStatus($"ffplay (PID {pFf.Id}).", AccentBlue); return; }
            }

            if (!ProcessRunner.OpenWithDefaultApp(url))
            {
                AppendLog("[ERROR] No se encontró VLC ni ffplay en el sistema.");
                MessageBox.Show("No se encontró VLC ni ffplay.\nInstálalos o añádelos al PATH.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnConfig_Click(object? sender, EventArgs e)
        {
            using var dlg = new SettingsForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                AppendLog("Configuración guardada.");
                MessageBox.Show("Configuración guardada.", "Config", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ── MediaPlayer event callbacks (cross-thread safe) ───────────────────────

        private void OnPlaying(object? sender, EventArgs e)
        {
            try { BeginInvoke(() => { SetStatus("Reproduciendo streaming.", AccentGreen); AppendLog("✔ Stream activo — reproduciendo."); _btnPlay.Enabled = false; _btnStop.Enabled = true; }); }
            catch { }
        }

        private void OnStopped(object? sender, EventArgs e)
        {
            try { BeginInvoke(() => { SetStatus("Stream detenido.", DimTextColor); AppendLog("Stream detenido."); _btnPlay.Enabled = true; _btnStop.Enabled = false; }); }
            catch { }
        }

        private void OnError(object? sender, EventArgs e)
        {
            try { BeginInvoke(() => { SetStatus("Error en el stream.", AccentRed); AppendLog("[ERROR] Error al reproducir el stream. Comprueba la URL y que MediaMTX esté activo."); _btnPlay.Enabled = true; _btnStop.Enabled = false; }); }
            catch { }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void AppendLog(string message)
        {
            try
            {
                if (InvokeRequired) { BeginInvoke(() => AppendLog(message)); return; }
                _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}]  {message}{Environment.NewLine}");
            }
            catch { }
        }

        private void SetStatus(string text, Color color)
        {
            _lblStatus.Text      = $"  Estado: {text}";
            _lblStatus.ForeColor = color;
        }

        // ── Styling ───────────────────────────────────────────────────────────────

        private static void StyleButton(Button btn, Color back, Color fore)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = back;
            btn.ForeColor = fore;
            btn.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            btn.Cursor    = Cursors.Hand;
            btn.Height    = 30;
            btn.Margin    = new Padding(3, 2, 3, 2);
        }

        private static void StyleTextBox(TextBox txt)
        {
            txt.BackColor   = Color.FromArgb(17, 17, 27);
            txt.ForeColor   = Color.FromArgb(198, 208, 245);
            txt.BorderStyle = BorderStyle.FixedSingle;
            txt.Font        = new Font("Consolas", 9F);
        }

        private static void StyleLabel(Label lbl)
        {
            lbl.ForeColor = Color.FromArgb(198, 208, 245);
            lbl.Font      = new Font("Segoe UI", 9F);
        }

        private static void StyleTrackBar(TrackBar tb)
        {
            tb.BackColor = Color.FromArgb(37, 37, 56);
        }

        // ── Cleanup ───────────────────────────────────────────────────────────────

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            try { _mediaPlayer?.Stop(); _mediaPlayer?.Dispose(); _libVLC?.Dispose(); _videoView?.Dispose(); } catch { }
            try { _udpListenerCts?.Cancel(); _udpListenerCts?.Dispose(); } catch { }
        }
    }
}
