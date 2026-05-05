using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MdView;

public class MainForm : Form
{
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly ToolStripButton _reloadBtn = new("Reload") { Enabled = false };
    private readonly ToolStripButton _viewSourceBtn = new("View Source") { Enabled = false };
    private readonly ToolStrip _toolStrip = new();
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
private readonly ScrollNotifyRtb _sourceView = new() { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Consolas", 12), WordWrap = false, BorderStyle = BorderStyle.None, BackColor = SystemColors.Window };
    private Panel _sourceContainer = null!;

    private FileSystemWatcher? _watcher;
    private string? _currentFile;
    private bool _reloadPending;
    private bool _webViewReady;

    public MainForm(string? initialFile)
    {
        Text = "Markdown Viewer";
        Size = new Size(1100, 800);
        MinimumSize = new Size(400, 300);
        Icon = LoadEmbeddedIcon();

        var openBtn = new ToolStripButton("Open…");
        openBtn.Click += (_, _) => PickFile();
        _reloadBtn.Click += async (_, _) => await RenderAsync();
        _viewSourceBtn.Click += async (_, _) => await ToggleSourceView();

        var toolStrip = new ToolStrip(openBtn, new ToolStripSeparator(), _reloadBtn, new ToolStripSeparator(), _viewSourceBtn)
        {
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(4, 2, 0, 2)
        };

        _sourceContainer = new Panel { Dock = DockStyle.Fill, Visible = false };
        var lineGutter = new LineGutter(_sourceView);
        var gutterSpacer = new Panel { Width = 10, Dock = DockStyle.Left, BackColor = SystemColors.Window };
        _sourceContainer.Controls.Add(_sourceView);
        _sourceContainer.Controls.Add(gutterSpacer);
        _sourceContainer.Controls.Add(lineGutter);
        _sourceView.Dock = DockStyle.Fill;

        Controls.Add(_webView);
        Controls.Add(_sourceContainer);
        Controls.Add(toolStrip);

        AllowDrop = true;
        DragEnter += (_, e) =>
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        DragDrop += async (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
                await OpenFileAsync(files[0]);
        };

        Load += async (_, _) =>
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarkdownViewer", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _webView.EnsureCoreWebView2Async(env);
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webViewReady = true;

            _webView.NavigateToString(BuildHtml("", null));

            if (initialFile != null)
                await OpenFileAsync(initialFile);
        };
    }

    private async Task OpenFileAsync(string path)
    {
        if (!File.Exists(path)) return;
        _currentFile = path;
        Text = Path.GetFileName(path) + " — Markdown Viewer";
        _reloadBtn.Enabled = true;
        _viewSourceBtn.Enabled = true;
        await RenderAsync();
        StartWatcher(path);
    }

    private async Task RenderAsync()
    {
        if (_currentFile == null || !_webViewReady) return;
        try
        {
            string md = await File.ReadAllTextAsync(_currentFile);
            string body = Markdown.ToHtml(md, _pipeline);
            string fullDir = Path.GetDirectoryName(Path.GetFullPath(_currentFile))!;
            string driveRoot = Path.GetPathRoot(fullDir)!;
            string relDir = fullDir.Substring(driveRoot.Length).Replace('\\', '/');

            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "mdview.local", driveRoot, CoreWebView2HostResourceAccessKind.Allow);

            _webView.NavigateToString(BuildHtml(body, $"https://mdview.local/{relDir}/"));
        }
        catch (Exception ex)
        {
            _webView.NavigateToString(BuildHtml(
                $"<pre class='error'>{WebUtility.HtmlEncode(ex.Message)}</pre>", null));
        }
    }

    private async Task ToggleSourceView()
    {
        if (_currentFile == null)
        {
            MessageBox.Show("Open a Markdown file first.", "No File", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_sourceContainer!.Visible)
        {
            _sourceContainer.Visible = false;
            _webView.Visible = true;
        }
        else
        {
            string md = await File.ReadAllTextAsync(_currentFile);
            _sourceView.Text = md;
            ColorizeSource(md);
            _webView.Visible = false;
            _sourceContainer.Visible = true;
        }
    }

    private void ColorizeSource(string md)
    {
        int saved = _sourceView.SelectionStart;
        _sourceView.SelectAll();
        _sourceView.SelectionColor = Color.Black;
        _sourceView.SelectionFont = _sourceView.Font;

        using var bold       = new Font("Consolas", 12, FontStyle.Bold);
        using var italic     = new Font("Consolas", 12, FontStyle.Italic);
        using var boldItalic = new Font("Consolas", 12, FontStyle.Bold | FontStyle.Italic);
        var reg = _sourceView.Font;

        void Apply(string pattern, Color color, Font font, RegexOptions opts = RegexOptions.None)
        {
            foreach (Match m in Regex.Matches(md, pattern, opts))
            {
                _sourceView.Select(m.Index, m.Length);
                _sourceView.SelectionColor = color;
                _sourceView.SelectionFont = font;
            }
        }

        // VS Code Light+ token colors
        var darkRed     = Color.FromArgb(0x80, 0x00, 0x00); // headings, code  #800000
        var navy        = Color.FromArgb(0x00, 0x00, 0x80); // bold             #000080
        var purple      = Color.FromArgb(0x80, 0x00, 0x80); // italic           #800080
        var blue        = Color.FromArgb(0x04, 0x51, 0xa5); // links, markers   #0451a5
        var green       = Color.FromArgb(0x6a, 0x99, 0x55); // blockquote       #6a9955
        var muted       = Color.FromArgb(0x80, 0x80, 0x80); // hr               #808080

        // Block-level first so inline can override where they overlap
        Apply(@"^#{1,6}[ \t]+.*$",                 darkRed, bold,   RegexOptions.Multiline);
        Apply(@"^[ \t]*>.*$",                       green,   italic, RegexOptions.Multiline);
        Apply(@"^[ \t]*(`{3}|~{3}).*$",            darkRed, reg,    RegexOptions.Multiline);
        Apply(@"^[ \t]*(\*{3}|-{3}|_{3})[ \t]*$", muted,   reg,    RegexOptions.Multiline);
        Apply(@"^[ \t]*[-*+][ \t]",                blue,    bold,   RegexOptions.Multiline);
        Apply(@"^[ \t]*\d+\.[ \t]",                blue,    bold,   RegexOptions.Multiline);

        // Inline
        Apply(@"`[^`\n]+`", darkRed, reg);
        Apply(@"\*{3}(?!\*)[^*\n]+\*{3}|_{3}(?!_)[^_\n]+_{3}", navy, boldItalic);
        Apply(@"(?<!\*)\*{2}(?!\*)[^*\n]+(?<!\*)\*{2}(?!\*)|(?<!_)_{2}(?!_)[^_\n]+(?<!_)_{2}(?!_)", navy, bold);
        Apply(@"(?<!\*)\*(?!\*)[^*\n]+(?<!\*)\*(?!\*)|(?<![_\w])_(?!_)[^_\n]+_(?![_\w])", purple, italic);
        Apply(@"\[[^\]\n]*\](?:\([^\)\n]*\)|\[[^\]\n]*\])", blue, reg);

        _sourceView.SelectionStart = Math.Min(saved, _sourceView.Text.Length);
        _sourceView.ScrollToCaret();
    }

    private void StartWatcher(string path)
    {
        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_reloadPending) return;
        _reloadPending = true;
        BeginInvoke(async () =>
        {
            try
            {
                await Task.Delay(300);
                var result = MessageBox.Show(
                    $"{Path.GetFileName(_currentFile)} was modified. Reload?",
                    "File Changed", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                    await RenderAsync();
            }
            finally { _reloadPending = false; }
        });
    }

    private void PickFile()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
            Title = "Open Markdown File"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _ = OpenFileAsync(dlg.FileName);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _watcher?.Dispose();
        base.OnFormClosed(e);
    }

    private static Icon? LoadEmbeddedIcon()
    {
        var stream = typeof(MainForm).Assembly.GetManifestResourceStream("MdView.appicon.ico");
        return stream is not null ? new Icon(stream) : null;
    }

    private static string BuildHtml(string body, string? baseHref)
    {
        string baseTag = baseHref is not null ? $"""<base href="{baseHref}">""" : "";
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            {baseTag}
            <style>{Css}</style>
            </head>
            <body>{body}</body>
            </html>
            """;
    }

    private sealed class ScrollNotifyRtb : RichTextBox
    {
        public event EventHandler? Scrolled;
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            const int WM_VSCROLL = 0x0115;
            const int WM_MOUSEWHEEL = 0x020A;
            if (m.Msg == WM_VSCROLL || m.Msg == WM_MOUSEWHEEL)
                Scrolled?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class LineGutter : Panel
    {
        private readonly RichTextBox _rtb;

        public LineGutter(ScrollNotifyRtb rtb)
        {
            _rtb = rtb;
            Width = 52;
            Dock = DockStyle.Left;
            BackColor = Color.FromArgb(245, 245, 245);
            DoubleBuffered = true;
            rtb.TextChanged += (_, _) => Invalidate();
            rtb.Scrolled += (_, _) => Invalidate();
            rtb.Resize += (_, _) => Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            using var pen = new Pen(Color.FromArgb(210, 210, 210));
            e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_rtb.Lines.Length == 0) return;
            using var brush = new SolidBrush(Color.FromArgb(140, 140, 140));
            var font = _rtb.Font;
            int firstChar = _rtb.GetCharIndexFromPosition(new Point(1, 1));
            int firstLine = _rtb.GetLineFromCharIndex(firstChar);
            for (int i = firstLine; i < _rtb.Lines.Length; i++)
            {
                int ci = _rtb.GetFirstCharIndexFromLine(i);
                if (ci < 0) break;
                Point p = _rtb.GetPositionFromCharIndex(ci);
                if (p.Y > _rtb.Height) break;
                string num = (i + 1).ToString();
                SizeF sz = e.Graphics.MeasureString(num, font);
                e.Graphics.DrawString(num, font, brush, Width - sz.Width - 6, p.Y);
            }
        }
    }

    private const string Css = """
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
            font-size: 16px; line-height: 1.6; color: #24292f; background: #fff;
            max-width: 860px; margin: 0 auto; padding: 32px 48px 64px;
        }
        h1,h2,h3,h4,h5,h6 { margin-top: 1.5em; margin-bottom: .5em; font-weight: 600; line-height: 1.25; }
        h1 { font-size: 2em;    border-bottom: 1px solid #d8dee4; padding-bottom: .3em; }
        h2 { font-size: 1.5em;  border-bottom: 1px solid #d8dee4; padding-bottom: .3em; }
        h3 { font-size: 1.25em; }
        p  { margin: .75em 0; }
        a  { color: #0969da; text-decoration: none; }
        a:hover { text-decoration: underline; }
        code {
            font-family: 'Cascadia Code', 'Fira Code', Consolas, monospace;
            font-size: 85%; background: #f6f8fa; padding: .2em .4em; border-radius: 6px;
        }
        pre { background: #f6f8fa; border-radius: 6px; padding: 16px; overflow: auto; margin: 1em 0; }
        pre code { background: transparent; padding: 0; font-size: 93%; }
        blockquote { padding: 0 1em; color: #57606a; border-left: 4px solid #d0d7de; margin: 1em 0; }
        table { border-collapse: collapse; width: 100%; margin: 1em 0; }
        th { background: #f6f8fa; font-weight: 600; }
        th, td { border: 1px solid #d0d7de; padding: 6px 13px; text-align: left; }
        tr:nth-child(even) td { background: #f6f8fa; }
        img { max-width: 100%; height: auto; }
        hr { border: none; border-top: 1px solid #d0d7de; margin: 1.5em 0; }
        ul, ol { padding-left: 2em; margin: .5em 0; }
        li { margin: .2em 0; }
        input[type=checkbox] { margin-right: .4em; }
        .placeholder { color: #999; text-align: center; margin-top: 80px; font-size: 1.1em; }
        .error { color: #d1242f; background: #fff0f0; padding: 16px; border-radius: 6px; }
        """;
}
