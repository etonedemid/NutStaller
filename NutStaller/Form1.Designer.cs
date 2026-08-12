using NutStaller.Ui;

namespace NutStaller
{
    partial class NutStallerMainWindow
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
            if (disposing && (components != null))
            {
                components.Dispose();
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NutStallerMainWindow));
            navSetup = new BkButton();
            navUpdate = new BkButton();
            navKeybinds = new BkButton();
            navPlay = new BkButton();
            pageSetup = new BkPagePanel();
            setupTitle = new BkLabel();
            installLabel = new BkLabel();
            installBox = new BkValueBox();
            browseButton = new BkButton();
            getRenutButton = new BkButton();
            renutStatus = new BkLabel();
            getXisoButton = new BkButton();
            xisoStatus = new BkLabel();
            extractButton = new BkButton();
            gameStatus = new BkLabel();
            doAllButton = new BkButton();
            progressBar = new BkProgress();
            setupStatus = new BkLabel();
            pageKeybinds = new BkPagePanel();
            keybindsTitle = new BkLabel();
            bindLogLabel = new BkLabel();
            bindLogField = new BkKeyField();
            keybindNote1 = new BkLabel();
            keybindNote2 = new BkLabel();
            saveKeybindsButton = new BkButton();
            keybindsStatus = new BkLabel();
            navCredits = new BkButton();
            pageCredits = new BkPagePanel();
            creditsTitle = new BkLabel();
            creditsLine1 = new BkLabel();
            creditsLine2 = new BkLabel();
            creditsLine3 = new BkLabel();
            creditsLine4 = new BkLabel();
            creditsLine5 = new BkLabel();
            creditsLine6 = new BkLabel();
            creditsLine7 = new BkLabel();
            creditsGithubButton = new BkButton();
            creditsDiscordButton = new BkButton();
            gamepadHint = new BkLabel();
            pageSetup.SuspendLayout();
            pageKeybinds.SuspendLayout();
            pageCredits.SuspendLayout();
            SuspendLayout();
            // 
            // navSetup
            // 
            navSetup.BackColor = Color.Transparent;
            navSetup.Location = new Point(402, 335);
            navSetup.Name = "navSetup";
            navSetup.Selected = true;
            navSetup.Size = new Size(185, 32);
            navSetup.TabIndex = 0;
            navSetup.Text = "Set Up Game";
            navSetup.Click += NavSetup_Click;
            // 
            // navUpdate
            // 
            navUpdate.BackColor = Color.Transparent;
            navUpdate.Location = new Point(402, 371);
            navUpdate.Name = "navUpdate";
            navUpdate.Size = new Size(185, 32);
            navUpdate.TabIndex = 1;
            navUpdate.Text = "Check Updates";
            navUpdate.Click += NavUpdate_Click;
            // 
            // navKeybinds
            // 
            navKeybinds.BackColor = Color.Transparent;
            navKeybinds.Location = new Point(402, 409);
            navKeybinds.Name = "navKeybinds";
            navKeybinds.Size = new Size(185, 32);
            navKeybinds.TabIndex = 2;
            navKeybinds.Text = "Keybinds";
            navKeybinds.Click += NavKeybinds_Click;
            // 
            // navPlay
            // 
            navPlay.BackColor = Color.Transparent;
            navPlay.FontSizePx = 24;
            navPlay.Location = new Point(402, 496);
            navPlay.Name = "navPlay";
            navPlay.Size = new Size(185, 36);
            navPlay.TabIndex = 3;
            navPlay.Text = "Play";
            navPlay.Click += NavPlay_Click;
            // 
            // pageSetup
            // 
            pageSetup.BackColor = Color.Transparent;
            pageSetup.Controls.Add(setupTitle);
            pageSetup.Controls.Add(installLabel);
            pageSetup.Controls.Add(installBox);
            pageSetup.Controls.Add(browseButton);
            pageSetup.Controls.Add(getRenutButton);
            pageSetup.Controls.Add(renutStatus);
            pageSetup.Controls.Add(getXisoButton);
            pageSetup.Controls.Add(xisoStatus);
            pageSetup.Controls.Add(extractButton);
            pageSetup.Controls.Add(gameStatus);
            pageSetup.Controls.Add(doAllButton);
            pageSetup.Controls.Add(progressBar);
            pageSetup.Controls.Add(setupStatus);
            pageSetup.Location = new Point(634, 281);
            pageSetup.Name = "pageSetup";
            pageSetup.Size = new Size(551, 323);
            pageSetup.TabIndex = 4;
            // 
            // setupTitle
            // 
            setupTitle.BackColor = Color.Transparent;
            setupTitle.FontSizePx = 26;
            setupTitle.Location = new Point(18, 16);
            setupTitle.Name = "setupTitle";
            setupTitle.Size = new Size(300, 30);
            setupTitle.TabIndex = 0;
            setupTitle.Text = "SET UP RENUT";
            // 
            // installLabel
            // 
            installLabel.BackColor = Color.Transparent;
            installLabel.Dim = true;
            installLabel.Location = new Point(19, 230);
            installLabel.Name = "installLabel";
            installLabel.Size = new Size(150, 30);
            installLabel.TabIndex = 1;
            installLabel.Text = "INSTALL FOLDER";
            // 
            // installBox
            // 
            installBox.BackColor = Color.Transparent;
            installBox.Location = new Point(175, 228);
            installBox.Name = "installBox";
            installBox.Size = new Size(299, 30);
            installBox.TabIndex = 2;
            installBox.Transparent = true;
            // 
            // browseButton
            // 
            browseButton.BackColor = Color.Transparent;
            browseButton.FontSizePx = 20;
            browseButton.Location = new Point(499, 230);
            browseButton.Name = "browseButton";
            browseButton.Size = new Size(40, 30);
            browseButton.TabIndex = 3;
            browseButton.Text = "...";
            browseButton.Click += BrowseButton_Click;
            // 
            // getRenutButton
            // 
            getRenutButton.BackColor = Color.Transparent;
            getRenutButton.FontSizePx = 20;
            getRenutButton.Location = new Point(19, 58);
            getRenutButton.Name = "getRenutButton";
            getRenutButton.Size = new Size(240, 32);
            getRenutButton.TabIndex = 4;
            getRenutButton.Text = "1. Get latest reNut";
            getRenutButton.Click += GetRenutButton_Click;
            // 
            // renutStatus
            // 
            renutStatus.BackColor = Color.Transparent;
            renutStatus.Dim = true;
            renutStatus.FontSizePx = 17;
            renutStatus.Location = new Point(268, 100);
            renutStatus.Name = "renutStatus";
            renutStatus.Size = new Size(284, 32);
            renutStatus.TabIndex = 5;
            // 
            // getXisoButton
            // 
            getXisoButton.BackColor = Color.Transparent;
            getXisoButton.FontSizePx = 20;
            getXisoButton.Location = new Point(19, 98);
            getXisoButton.Name = "getXisoButton";
            getXisoButton.Size = new Size(240, 32);
            getXisoButton.TabIndex = 6;
            getXisoButton.Text = "2. Get extract-xiso";
            getXisoButton.Click += GetXisoButton_Click;
            // 
            // xisoStatus
            // 
            xisoStatus.BackColor = Color.Transparent;
            xisoStatus.Dim = true;
            xisoStatus.FontSizePx = 17;
            xisoStatus.Location = new Point(268, 140);
            xisoStatus.Name = "xisoStatus";
            xisoStatus.Size = new Size(284, 32);
            xisoStatus.TabIndex = 7;
            // 
            // extractButton
            // 
            extractButton.BackColor = Color.Transparent;
            extractButton.FontSizePx = 20;
            extractButton.Location = new Point(19, 138);
            extractButton.Name = "extractButton";
            extractButton.Size = new Size(240, 32);
            extractButton.TabIndex = 8;
            extractButton.Text = "3. Pick ISO + extract";
            extractButton.Click += ExtractButton_Click;
            // 
            // gameStatus
            // 
            gameStatus.BackColor = Color.Transparent;
            gameStatus.Dim = true;
            gameStatus.FontSizePx = 17;
            gameStatus.Location = new Point(268, 180);
            gameStatus.Name = "gameStatus";
            gameStatus.Size = new Size(284, 32);
            gameStatus.TabIndex = 9;
            // 
            // doAllButton
            // 
            doAllButton.BackColor = Color.Transparent;
            doAllButton.Location = new Point(19, 178);
            doAllButton.Name = "doAllButton";
            doAllButton.Size = new Size(240, 34);
            doAllButton.TabIndex = 10;
            doAllButton.Text = "Do everything";
            doAllButton.Click += DoAllButton_Click;
            // 
            // progressBar
            // 
            progressBar.BackColor = Color.Green;
            progressBar.Location = new Point(19, 267);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(523, 32);
            progressBar.TabIndex = 11;
            // 
            // setupStatus
            // 
            setupStatus.BackColor = Color.Transparent;
            setupStatus.Dim = true;
            setupStatus.FontSizePx = 17;
            setupStatus.Location = new Point(16, 290);
            setupStatus.Name = "setupStatus";
            setupStatus.Size = new Size(536, 52);
            setupStatus.TabIndex = 12;
            // 
            // pageKeybinds
            // 
            pageKeybinds.BackColor = Color.Transparent;
            pageKeybinds.Controls.Add(keybindsTitle);
            pageKeybinds.Controls.Add(bindLogLabel);
            pageKeybinds.Controls.Add(bindLogField);
            pageKeybinds.Controls.Add(keybindNote1);
            pageKeybinds.Controls.Add(keybindNote2);
            pageKeybinds.Controls.Add(saveKeybindsButton);
            pageKeybinds.Controls.Add(keybindsStatus);
            pageKeybinds.Location = new Point(634, 279);
            pageKeybinds.Name = "pageKeybinds";
            pageKeybinds.Size = new Size(545, 382);
            pageKeybinds.TabIndex = 6;
            pageKeybinds.Visible = false;
            // 
            // keybindsTitle
            // 
            keybindsTitle.BackColor = Color.Transparent;
            keybindsTitle.FontSizePx = 26;
            keybindsTitle.Location = new Point(16, 14);
            keybindsTitle.Name = "keybindsTitle";
            keybindsTitle.Size = new Size(300, 30);
            keybindsTitle.TabIndex = 0;
            keybindsTitle.Text = "KEYBINDS";
            // 
            // bindLogLabel
            // 
            bindLogLabel.BackColor = Color.Transparent;
            bindLogLabel.Dim = true;
            bindLogLabel.Location = new Point(16, 58);
            bindLogLabel.Name = "bindLogLabel";
            bindLogLabel.Size = new Size(220, 30);
            bindLogLabel.TabIndex = 1;
            bindLogLabel.Text = "LOG OVERLAY";
            // 
            // bindLogField
            // 
            bindLogField.BackColor = Color.Transparent;
            bindLogField.KeyName = "F2";
            bindLogField.Location = new Point(246, 58);
            bindLogField.Name = "bindLogField";
            bindLogField.Size = new Size(140, 30);
            bindLogField.TabIndex = 2;
            // 
            // keybindNote1
            // 
            keybindNote1.BackColor = Color.Transparent;
            keybindNote1.Dim = true;
            keybindNote1.FontSizePx = 15;
            keybindNote1.Location = new Point(16, 106);
            keybindNote1.Name = "keybindNote1";
            keybindNote1.Size = new Size(400, 24);
            keybindNote1.TabIndex = 3;
            keybindNote1.Text = "F4 OPENS THE IN-GAME CVAR MENU";
            // 
            // keybindNote2
            // 
            keybindNote2.BackColor = Color.Transparent;
            keybindNote2.Dim = true;
            keybindNote2.FontSizePx = 15;
            keybindNote2.Location = new Point(16, 132);
            keybindNote2.Name = "keybindNote2";
            keybindNote2.Size = new Size(520, 24);
            keybindNote2.TabIndex = 4;
            keybindNote2.Text = "MORE BINDS SHOW UP HERE ONCE THE GAME WRITES THEM";
            // 
            // saveKeybindsButton
            // 
            saveKeybindsButton.BackColor = Color.Transparent;
            saveKeybindsButton.FontSizePx = 20;
            saveKeybindsButton.Location = new Point(16, 336);
            saveKeybindsButton.Name = "saveKeybindsButton";
            saveKeybindsButton.Size = new Size(110, 32);
            saveKeybindsButton.TabIndex = 5;
            saveKeybindsButton.Text = "Save";
            saveKeybindsButton.Click += SaveKeybindsButton_Click;
            // 
            // keybindsStatus
            // 
            keybindsStatus.BackColor = Color.Transparent;
            keybindsStatus.Dim = true;
            keybindsStatus.FontSizePx = 16;
            keybindsStatus.Location = new Point(136, 336);
            keybindsStatus.Name = "keybindsStatus";
            keybindsStatus.Size = new Size(320, 32);
            keybindsStatus.TabIndex = 6;
            // 
            // navCredits
            // 
            navCredits.BackColor = Color.Transparent;
            navCredits.Location = new Point(402, 445);
            navCredits.Name = "navCredits";
            navCredits.Size = new Size(185, 32);
            navCredits.TabIndex = 7;
            navCredits.Text = "Credits";
            navCredits.Click += NavCredits_Click;
            // 
            // pageCredits
            // 
            pageCredits.BackColor = Color.Transparent;
            pageCredits.Controls.Add(creditsTitle);
            pageCredits.Controls.Add(creditsLine1);
            pageCredits.Controls.Add(creditsLine2);
            pageCredits.Controls.Add(creditsLine3);
            pageCredits.Controls.Add(creditsLine4);
            pageCredits.Controls.Add(creditsLine5);
            pageCredits.Controls.Add(creditsLine6);
            pageCredits.Controls.Add(creditsLine7);
            pageCredits.Controls.Add(creditsGithubButton);
            pageCredits.Controls.Add(creditsDiscordButton);
            pageCredits.Controls.Add(gamepadHint);
            pageCredits.Location = new Point(634, 281);
            pageCredits.Name = "pageCredits";
            pageCredits.Size = new Size(548, 382);
            pageCredits.TabIndex = 8;
            pageCredits.Visible = false;
            // 
            // creditsTitle
            // 
            creditsTitle.BackColor = Color.Transparent;
            creditsTitle.FontSizePx = 26;
            creditsTitle.Location = new Point(16, 14);
            creditsTitle.Name = "creditsTitle";
            creditsTitle.Size = new Size(300, 30);
            creditsTitle.TabIndex = 0;
            creditsTitle.Text = "CREDITS";
            // 
            // creditsLine1
            // 
            creditsLine1.BackColor = Color.Transparent;
            creditsLine1.Location = new Point(16, 56);
            creditsLine1.Name = "creditsLine1";
            creditsLine1.Size = new Size(516, 26);
            creditsLine1.TabIndex = 1;
            creditsLine1.Text = "RENUT BY MASTERSPIKE52 AND CONTRIBUTORS";
            // 
            // creditsLine2
            // 
            creditsLine2.BackColor = Color.Transparent;
            creditsLine2.Dim = true;
            creditsLine2.FontSizePx = 16;
            creditsLine2.Location = new Point(16, 88);
            creditsLine2.Name = "creditsLine2";
            creditsLine2.Size = new Size(516, 24);
            creditsLine2.TabIndex = 2;
            creditsLine2.Text = "REXGLUE TEAM - REXGLUE SDK";
            // 
            // creditsLine3
            // 
            creditsLine3.BackColor = Color.Transparent;
            creditsLine3.Dim = true;
            creditsLine3.FontSizePx = 16;
            creditsLine3.Location = new Point(16, 114);
            creditsLine3.Name = "creditsLine3";
            creditsLine3.Size = new Size(516, 24);
            creditsLine3.TabIndex = 3;
            creditsLine3.Text = "SOLARCOOKIES - MIDASM HOOKS AND CRT FUNCTIONS";
            // 
            // creditsLine4
            // 
            creditsLine4.BackColor = Color.Transparent;
            creditsLine4.Dim = true;
            creditsLine4.FontSizePx = 16;
            creditsLine4.Location = new Point(16, 140);
            creditsLine4.Name = "creditsLine4";
            creditsLine4.Size = new Size(516, 24);
            creditsLine4.TabIndex = 4;
            creditsLine4.Text = "VALCOMDRIFTY - RENUT LOGO";
            // 
            // creditsLine5
            // 
            creditsLine5.BackColor = Color.Transparent;
            creditsLine5.Dim = true;
            creditsLine5.FontSizePx = 16;
            creditsLine5.Location = new Point(16, 166);
            creditsLine5.Name = "creditsLine5";
            creditsLine5.Size = new Size(516, 24);
            creditsLine5.TabIndex = 5;
            creditsLine5.Text = "OLIEGAMERTV - NUTS AND BOLTS DOCUMENTATION";
            // 
            // creditsLine6
            // 
            creditsLine6.BackColor = Color.Transparent;
            creditsLine6.Dim = true;
            creditsLine6.FontSizePx = 16;
            creditsLine6.Location = new Point(16, 192);
            creditsLine6.Name = "creditsLine6";
            creditsLine6.Size = new Size(516, 24);
            creditsLine6.TabIndex = 6;
            creditsLine6.Text = "EXTRACT-XISO BY THE XBOXDEV TEAM";
            // 
            // creditsLine7
            // 
            creditsLine7.BackColor = Color.Transparent;
            creditsLine7.Dim = true;
            creditsLine7.FontSizePx = 16;
            creditsLine7.Location = new Point(16, 218);
            creditsLine7.Name = "creditsLine7";
            creditsLine7.Size = new Size(516, 24);
            creditsLine7.TabIndex = 7;
            creditsLine7.Text = "THIS - ETONEDEMID";
            // 
            // creditsGithubButton
            // 
            creditsGithubButton.BackColor = Color.Transparent;
            creditsGithubButton.FontSizePx = 20;
            creditsGithubButton.Location = new Point(16, 268);
            creditsGithubButton.Name = "creditsGithubButton";
            creditsGithubButton.Size = new Size(240, 32);
            creditsGithubButton.TabIndex = 8;
            creditsGithubButton.Text = "reNut on GitHub";
            creditsGithubButton.Click += CreditsGithubButton_Click;
            // 
            // creditsDiscordButton
            // 
            creditsDiscordButton.BackColor = Color.Transparent;
            creditsDiscordButton.FontSizePx = 20;
            creditsDiscordButton.Location = new Point(268, 268);
            creditsDiscordButton.Name = "creditsDiscordButton";
            creditsDiscordButton.Size = new Size(240, 32);
            creditsDiscordButton.TabIndex = 9;
            creditsDiscordButton.Text = "Join the Discord";
            creditsDiscordButton.Click += CreditsDiscordButton_Click;
            // 
            // gamepadHint
            // 
            gamepadHint.BackColor = Color.Transparent;
            gamepadHint.Dim = true;
            gamepadHint.FontSizePx = 14;
            gamepadHint.Location = new Point(16, 330);
            gamepadHint.Name = "gamepadHint";
            gamepadHint.Size = new Size(516, 24);
            gamepadHint.TabIndex = 10;
            gamepadHint.Text = "GAMEPAD: DPAD MOVE - A SELECT - LB/RB SWITCH PAGE - START PLAY";
            // 
            // NutStallerMainWindow
            // 
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(45, 82, 168);
            BackgroundImage = (Image)resources.GetObject("$this.BackgroundImage");
            ClientSize = new Size(1200, 675);
            Controls.Add(navSetup);
            Controls.Add(navUpdate);
            Controls.Add(navKeybinds);
            Controls.Add(navPlay);
            Controls.Add(pageSetup);
            Controls.Add(pageKeybinds);
            Controls.Add(navCredits);
            Controls.Add(pageCredits);
            MinimumSize = new Size(816, 549);
            Name = "NutStallerMainWindow";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "NutStaller";
            pageSetup.ResumeLayout(false);
            pageKeybinds.ResumeLayout(false);
            pageCredits.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private BkButton navSetup;
        private BkButton navUpdate;
        private BkButton navKeybinds;
        private BkButton navPlay;
        private BkPagePanel pageSetup;
        private BkLabel setupTitle;
        private BkLabel installLabel;
        private BkValueBox installBox;
        private BkButton browseButton;
        private BkButton getRenutButton;
        private BkLabel renutStatus;
        private BkButton getXisoButton;
        private BkLabel xisoStatus;
        private BkButton extractButton;
        private BkLabel gameStatus;
        private BkButton doAllButton;
        private BkLabel setupStatus;
        private BkPagePanel pageKeybinds;
        private BkLabel keybindsTitle;
        private BkLabel bindLogLabel;
        private BkKeyField bindLogField;
        private BkLabel keybindNote1;
        private BkLabel keybindNote2;
        private BkButton saveKeybindsButton;
        private BkLabel keybindsStatus;
        private BkProgress progressBar;
        private BkButton navCredits;
        private BkPagePanel pageCredits;
        private BkLabel creditsTitle;
        private BkLabel creditsLine1;
        private BkLabel creditsLine2;
        private BkLabel creditsLine3;
        private BkLabel creditsLine4;
        private BkLabel creditsLine5;
        private BkLabel creditsLine6;
        private BkLabel creditsLine7;
        private BkButton creditsGithubButton;
        private BkButton creditsDiscordButton;
        private BkLabel gamepadHint;
    }
}
