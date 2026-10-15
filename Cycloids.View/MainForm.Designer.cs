namespace Cycloids.View;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // Элементы ввода (InputPanel по ТЗ)
    private GroupBox    groupBoxInputs;
    private Label       labelVLinWheel;
    private TextBox     textBoxVLinWheel;
    private Label       labelRWheel;
    private TextBox     textBoxRWheel;
    private Label       labelRDot;
    private TextBox     textBoxRDot;
    private Label       labelDt;
    private TextBox     textBoxDt;
    private Label       labelTEnd;
    private TextBox     textBoxTEnd;
    private Button      buttonRun;
    private Button      buttonExport;

    // Панель информации и аналитики (InfoPanel по ТЗ)
    private GroupBox    groupBoxInfo;
    private Label       labelStatus;
    private Label       labelKind;
    private Label       labelPeriod;
    private Label       labelDistance;
    private Label       labelSpeed04;
    private Label       labelSpeed08;

    // Область построения графика (PlotArea по ТЗ)
    private ScottPlot.WinForms.FormsPlot formsPlot1;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        groupBoxInputs   = new GroupBox();
        labelVLinWheel   = new Label();
        textBoxVLinWheel = new TextBox();
        labelRWheel      = new Label();
        textBoxRWheel    = new TextBox();
        labelRDot        = new Label();
        textBoxRDot      = new TextBox();
        labelDt          = new Label();
        textBoxDt        = new TextBox();
        labelTEnd        = new Label();
        textBoxTEnd      = new TextBox();
        buttonRun        = new Button();
        buttonExport     = new Button();

        groupBoxInfo     = new GroupBox();
        labelStatus      = new Label();
        labelKind        = new Label();
        labelPeriod      = new Label();
        labelDistance    = new Label();
        labelSpeed04     = new Label();
        labelSpeed08     = new Label();

        formsPlot1       = new ScottPlot.WinForms.FormsPlot();

        SuspendLayout();

        // ── Настройка окна ─────────────────────────────────────
        Text          = "Циклоида: точка на ободе колеса (Вариант 7)";
        Size          = new Size(1160, 720);
        MinimumSize   = new Size(950, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font          = new Font("Segoe UI", 9.25f);

        // ── 1. Блок ввода (InputPanel) ─────────────────────────
        groupBoxInputs.Text     = "Параметры колеса (InputPanel)";
        groupBoxInputs.Location = new Point(12, 12);
        groupBoxInputs.Size     = new Size(270, 275);
        groupBoxInputs.Font     = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        int AddInputRow(Label lbl, TextBox txt, string title, string defaultVal, string hint, int y)
        {
            lbl.Text      = title;
            lbl.Location  = new Point(12, y + 2);
            lbl.Size      = new Size(140, 22);
            lbl.Font      = new Font("Segoe UI", 9f);

            txt.Text      = defaultVal;
            txt.Location  = new Point(155, y);
            txt.Size      = new Size(100, 24);
            txt.Font      = new Font("Segoe UI", 9f);

            var tt = new ToolTip();
            tt.SetToolTip(txt, hint);

            groupBoxInputs.Controls.Add(lbl);
            groupBoxInputs.Controls.Add(txt);
            return y + 34;
        }

        int rowY = 26;
        rowY = AddInputRow(labelVLinWheel,   textBoxVLinWheel, "vLinWheel (м/с):", "10",   "Скорость оси колеса (может быть отрицательной или 0)", rowY);
        rowY = AddInputRow(labelRWheel,      textBoxRWheel,    "rWheel (м):",      "1.0",  "Радиус колеса (строго > 0)", rowY);
        rowY = AddInputRow(labelRDot,        textBoxRDot,      "rDot (м):",        "0.5",  "Расстояние от оси до точки (строго > 0)", rowY);
        rowY = AddInputRow(labelDt,          textBoxDt,        "dt (с):",          "0.01", "Шаг записи точек по времени", rowY);
        rowY = AddInputRow(labelTEnd,        textBoxTEnd,      "tEnd (с):",        "2.0",  "Общая длительность моделирования", rowY);

        // Кнопка Запустить
        buttonRun.Text      = "▶  Запустить расчёт";
        buttonRun.Location  = new Point(12, rowY + 5);
        buttonRun.Size      = new Size(243, 34);
        buttonRun.BackColor = Color.FromArgb(24, 118, 209);
        buttonRun.ForeColor = Color.White;
        buttonRun.Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        buttonRun.FlatStyle = FlatStyle.Flat;
        buttonRun.FlatAppearance.BorderSize = 0;
        buttonRun.Cursor    = Cursors.Hand;
        buttonRun.Click    += buttonRun_Click;
        groupBoxInputs.Controls.Add(buttonRun);

        // ── 2. Блок информации и аналитики (InfoPanel) ──────────
        groupBoxInfo.Text     = "Аналитика и статус (InfoPanel)";
        groupBoxInfo.Location = new Point(12, 295);
        groupBoxInfo.Size     = new Size(270, 310);
        groupBoxInfo.Font     = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        int infoY = 24;
        void AddInfoLabel(Label lbl, string defaultText, bool bold = false)
        {
            lbl.Text      = defaultText;
            lbl.Location  = new Point(12, infoY);
            lbl.Size      = new Size(245, 32);
            lbl.Font      = new Font("Segoe UI", 8.75f, bold ? FontStyle.Bold : FontStyle.Regular);
            lbl.ForeColor = Color.FromArgb(40, 40, 40);
            groupBoxInfo.Controls.Add(lbl);
            infoY += 34;
        }

        AddInfoLabel(labelKind,     "Тип циклоиды: —");
        AddInfoLabel(labelPeriod,   "Период оборота T: —");
        AddInfoLabel(labelDistance, "Смещение за оборот: —");
        AddInfoLabel(labelSpeed04,  "Скорость в t=0.4 с: —", true);
        AddInfoLabel(labelSpeed08,  "Скорость в t=0.8 с: —", true);

        labelStatus.Text      = "Статус: ожидание ввода параметров...";
        labelStatus.Location  = new Point(12, infoY + 5);
        labelStatus.Size      = new Size(245, 55);
        labelStatus.Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic);
        labelStatus.ForeColor = Color.FromArgb(90, 90, 90);
        groupBoxInfo.Controls.Add(labelStatus);

        // Кнопка Экспорт
        buttonExport.Text      = "💾  Экспорт в CSV";
        buttonExport.Location  = new Point(12, 615);
        buttonExport.Size      = new Size(270, 34);
        buttonExport.BackColor = Color.FromArgb(46, 125, 50);
        buttonExport.ForeColor = Color.White;
        buttonExport.Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        buttonExport.FlatStyle = FlatStyle.Flat;
        buttonExport.FlatAppearance.BorderSize = 0;
        buttonExport.Cursor    = Cursors.Hand;
        buttonExport.Enabled   = false;
        buttonExport.Click    += buttonExport_Click;

        // ── 3. Область построения графика (PlotArea) ───────────
        formsPlot1.Location = new Point(295, 12);
        formsPlot1.Size     = new Size(835, 637);
        formsPlot1.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom
                            | AnchorStyles.Left | AnchorStyles.Right;

        // Добавление на форму
        Controls.Add(groupBoxInputs);
        Controls.Add(groupBoxInfo);
        Controls.Add(buttonExport);
        Controls.Add(formsPlot1);

        ResumeLayout(false);
    }
}
