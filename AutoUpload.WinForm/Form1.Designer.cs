using System;

namespace AutoUpload.WinForm
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    notifyIcon?.Dispose();
                    timer?.Dispose();
                    components?.Dispose();
                    watcher?.Dispose();
                }

                // 释放非托管资源
                disposed = true;
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            splitContainer1 = new SplitContainer();
            flowLayoutPanel1 = new FlowLayoutPanel();
            btnSetting = new Button();
            btnLog = new Button();
            btnUpload = new Button();
            btnExit = new Button();
            tabControl = new TabControl();
            tabPageSetting = new TabPage();
            labelPathHint = new Label();
            labelPath = new Label();
            txtPath = new TextBox();
            btnBrowsePath = new Button();
            tabPageLog = new TabPage();
            tabPageUpload = new TabPage();
            labelPendingUpload = new Label();
            listBoxPendingUpload = new ListBox();
            labelUploadComplete = new Label();
            listBoxUploadComplete = new ListBox();
            labelUploadHint = new Label();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            flowLayoutPanel1.SuspendLayout();
            tabControl.SuspendLayout();
            tabPageSetting.SuspendLayout();
            tabPageUpload.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Margin = new Padding(2);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(flowLayoutPanel1);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(tabControl);
            splitContainer1.Size = new Size(934, 461);
            splitContainer1.SplitterDistance = 161;
            splitContainer1.SplitterWidth = 5;
            splitContainer1.TabIndex = 0;
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.AutoSize = true;
            flowLayoutPanel1.Controls.Add(btnSetting);
            flowLayoutPanel1.Controls.Add(btnLog);
            flowLayoutPanel1.Controls.Add(btnUpload);
            flowLayoutPanel1.Controls.Add(btnExit);
            flowLayoutPanel1.Dock = DockStyle.Left;
            flowLayoutPanel1.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel1.Location = new Point(0, 0);
            flowLayoutPanel1.Margin = new Padding(0);
            flowLayoutPanel1.MaximumSize = new Size(160, 0);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Padding = new Padding(20, 10, 20, 10);
            flowLayoutPanel1.Size = new Size(160, 461);
            flowLayoutPanel1.TabIndex = 4;
            // 
            // btnSetting
            // 
            btnSetting.Location = new Point(22, 12);
            btnSetting.Margin = new Padding(2);
            btnSetting.Name = "btnSetting";
            btnSetting.Size = new Size(116, 25);
            btnSetting.TabIndex = 2;
            btnSetting.Text = "设置";
            btnSetting.UseVisualStyleBackColor = true;
            btnSetting.Click += btnSetting_Click;
            // 
            // btnLog
            // 
            btnLog.Location = new Point(22, 41);
            btnLog.Margin = new Padding(2);
            btnLog.Name = "btnLog";
            btnLog.Size = new Size(116, 25);
            btnLog.TabIndex = 3;
            btnLog.Text = "日志";
            btnLog.UseVisualStyleBackColor = true;
            btnLog.Click += btnLog_Click;
            // 
            // btnUpload
            // 
            btnUpload.Location = new Point(22, 70);
            btnUpload.Margin = new Padding(2);
            btnUpload.Name = "btnUpload";
            btnUpload.Size = new Size(116, 25);
            btnUpload.TabIndex = 0;
            btnUpload.Text = "上传";
            btnUpload.UseVisualStyleBackColor = true;
            btnUpload.Click += btnUpload_Click;
            // 
            // btnExit
            // 
            btnExit.Location = new Point(22, 99);
            btnExit.Margin = new Padding(2);
            btnExit.Name = "btnExit";
            btnExit.Size = new Size(116, 25);
            btnExit.TabIndex = 1;
            btnExit.Text = "退出";
            btnExit.UseVisualStyleBackColor = true;
            btnExit.Click += btnExit_Click;
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabPageSetting);
            tabControl.Controls.Add(tabPageLog);
            tabControl.Controls.Add(tabPageUpload);
            tabControl.Dock = DockStyle.Fill;
            tabControl.Location = new Point(0, 0);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(768, 461);
            tabControl.TabIndex = 0;
            // 
            // tabPageSetting
            // 
            tabPageSetting.Controls.Add(labelPathHint);
            tabPageSetting.Controls.Add(labelPath);
            tabPageSetting.Controls.Add(txtPath);
            tabPageSetting.Controls.Add(btnBrowsePath);
            tabPageSetting.Location = new Point(4, 26);
            tabPageSetting.Name = "tabPageSetting";
            tabPageSetting.Padding = new Padding(3);
            tabPageSetting.Size = new Size(760, 431);
            tabPageSetting.TabIndex = 0;
            tabPageSetting.Text = "设置";
            tabPageSetting.UseVisualStyleBackColor = true;
            // 
            // labelPathHint
            // 
            labelPathHint.Dock = DockStyle.Bottom;
            labelPathHint.Font = new Font("Microsoft YaHei UI", 12F);
            labelPathHint.Location = new Point(3, 328);
            labelPathHint.MinimumSize = new Size(700, 0);
            labelPathHint.Name = "labelPathHint";
            labelPathHint.Size = new Size(754, 100);
            labelPathHint.TabIndex = 3;
            labelPathHint.TextAlign = ContentAlignment.TopCenter;
            // 
            // labelPath
            // 
            labelPath.AutoSize = true;
            labelPath.Location = new Point(33, 29);
            labelPath.Name = "labelPath";
            labelPath.Size = new Size(56, 17);
            labelPath.TabIndex = 2;
            labelPath.Text = "文件目录";
            // 
            // txtPath
            // 
            txtPath.Location = new Point(94, 27);
            txtPath.Margin = new Padding(2);
            txtPath.Name = "txtPath";
            txtPath.Size = new Size(252, 23);
            txtPath.TabIndex = 0;
            // 
            // btnBrowsePath
            // 
            btnBrowsePath.Location = new Point(350, 25);
            btnBrowsePath.Margin = new Padding(2);
            btnBrowsePath.Name = "btnBrowsePath";
            btnBrowsePath.Size = new Size(27, 25);
            btnBrowsePath.TabIndex = 1;
            btnBrowsePath.Text = "...";
            btnBrowsePath.UseVisualStyleBackColor = true;
            btnBrowsePath.Click += btnBrowsePath_Click;
            // 
            // tabPageLog
            // 
            tabPageLog.Location = new Point(4, 26);
            tabPageLog.Name = "tabPageLog";
            tabPageLog.Padding = new Padding(3);
            tabPageLog.Size = new Size(760, 431);
            tabPageLog.TabIndex = 1;
            tabPageLog.Text = "日志";
            tabPageLog.UseVisualStyleBackColor = true;
            // 
            // tabPageUpload
            // 
            tabPageUpload.Controls.Add(labelUploadHint);
            tabPageUpload.Controls.Add(labelPendingUpload);
            tabPageUpload.Controls.Add(listBoxPendingUpload);
            tabPageUpload.Controls.Add(labelUploadComplete);
            tabPageUpload.Controls.Add(listBoxUploadComplete);
            tabPageUpload.Location = new Point(4, 26);
            tabPageUpload.Name = "tabPageUpload";
            tabPageUpload.Padding = new Padding(3);
            tabPageUpload.Size = new Size(760, 431);
            tabPageUpload.TabIndex = 2;
            tabPageUpload.Text = "上传";
            tabPageUpload.UseVisualStyleBackColor = true;
            // 
            // labelPendingUpload
            // 
            labelPendingUpload.AutoSize = true;
            labelPendingUpload.Location = new Point(6, 133);
            labelPendingUpload.Name = "labelPendingUpload";
            labelPendingUpload.Size = new Size(56, 17);
            labelPendingUpload.TabIndex = 3;
            labelPendingUpload.Text = "等待上传";
            // 
            // listBoxPendingUpload
            // 
            listBoxPendingUpload.BorderStyle = BorderStyle.FixedSingle;
            listBoxPendingUpload.FormattingEnabled = true;
            listBoxPendingUpload.HorizontalScrollbar = true;
            listBoxPendingUpload.ItemHeight = 17;
            listBoxPendingUpload.Location = new Point(68, 133);
            listBoxPendingUpload.Name = "listBoxPendingUpload";
            listBoxPendingUpload.ScrollAlwaysVisible = true;
            listBoxPendingUpload.Size = new Size(213, 121);
            listBoxPendingUpload.TabIndex = 2;
            // 
            // labelUploadComplete
            // 
            labelUploadComplete.AutoSize = true;
            labelUploadComplete.Location = new Point(6, 6);
            labelUploadComplete.Name = "labelUploadComplete";
            labelUploadComplete.Size = new Size(56, 17);
            labelUploadComplete.TabIndex = 1;
            labelUploadComplete.Text = "上传成功";
            // 
            // listBoxUploadComplete
            // 
            listBoxUploadComplete.BorderStyle = BorderStyle.FixedSingle;
            listBoxUploadComplete.FormattingEnabled = true;
            listBoxUploadComplete.HorizontalScrollbar = true;
            listBoxUploadComplete.ItemHeight = 17;
            listBoxUploadComplete.Location = new Point(68, 6);
            listBoxUploadComplete.Name = "listBoxUploadComplete";
            listBoxUploadComplete.ScrollAlwaysVisible = true;
            listBoxUploadComplete.Size = new Size(213, 121);
            listBoxUploadComplete.TabIndex = 0;
            // 
            // labelUploadHint
            // 
            labelUploadHint.Dock = DockStyle.Bottom;
            labelUploadHint.Font = new Font("Microsoft YaHei UI", 12F);
            labelUploadHint.Location = new Point(3, 328);
            labelUploadHint.MinimumSize = new Size(700, 0);
            labelUploadHint.Name = "labelUploadHint";
            labelUploadHint.Size = new Size(754, 100);
            labelUploadHint.TabIndex = 4;
            labelUploadHint.TextAlign = ContentAlignment.TopCenter;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(934, 461);
            Controls.Add(splitContainer1);
            Margin = new Padding(2);
            MinimumSize = new Size(950, 500);
            Name = "Form1";
            Text = "Form1";
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel1.PerformLayout();
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            flowLayoutPanel1.ResumeLayout(false);
            tabControl.ResumeLayout(false);
            tabPageSetting.ResumeLayout(false);
            tabPageSetting.PerformLayout();
            tabPageUpload.ResumeLayout(false);
            tabPageUpload.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private SplitContainer splitContainer1;
        private Button btnBrowsePath;
        private TextBox txtPath;
        private Button btnUpload;

        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem menuShow;
        private System.Windows.Forms.ToolStripMenuItem menuExit;
        private Button btnExit;
        private Button btnLog;
        private Button btnSetting;
        private Label labelPath;
        private TabControl tabControl;
        private TabPage tabPageSetting;
        private TabPage tabPageLog;
        private TabPage tabPageUpload;
        private FlowLayoutPanel flowLayoutPanel1;
        private Label labelPathHint;
        private ListBox listBoxUploadComplete;
        private Label labelUploadComplete;
        private Label labelPendingUpload;
        private ListBox listBoxPendingUpload;
        private Label labelUploadHint;
    }
}
