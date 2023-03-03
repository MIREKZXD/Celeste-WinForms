using Celeste_WinForms.Properties;
using System;
using System.IO;
using System.Reflection;
using NAudio.Wave;

namespace Celeste_WinForms;

public partial class MainWindow : Form
{
    // Glob�ln� prom�nn�
    /// Hr��
    bool inputEnabled = false;

    bool left, right;
    bool leftInput, rightInput;
    bool upInput, downInput;

    bool jump, jumpInput, spaceReleased = true;
    bool slide;
    bool grab, grabInput;

    string facing = "", lastStraightFacing = "Right";
    bool midAir;
    bool jumpCooldown = false;
    bool grabAfterJumpCooldown = false;
    int movementSpeed;
    int movementSpeedTarget = 0;
    int movementSpeedMax;
    int movementSpeedMaxTarget = 6;
    int force;

    bool closeToBlockLeft, closeToBlockRight;
    int closeToBlockLeftDist, closeToBlockRightDist;
    bool onBlockLeft, onBlockRight;
    bool onBlockDown;
    int playerBlockHeightDiff;
    bool climbed;
    string lastGrabbedOn = "";

    bool dashInput, ctrlReleased = true;
    bool dashed;
    bool dashedNonVertical;

    int playerLeftOffset, playerRightOffset;

    /// Level
    int currentLevel = 1;

    Terrain[] terrainArray;

    /// Zvuky
    bool grabbedOn = false;
    bool touchedGround = true;

    /// Kamera
    int cameraMovementSpeed, cameraMovementSpeedTarget;
    int playerCenterY;


    bool developerKeys;   // NumPad0 stisknuta

    /// Tagy <summary>
    /// "collision" - na objekt se vztahuj� kolize
    /// "spring" - pru�ina
    /// </summary>

    public MainWindow()
    {
        InitializeComponent();
        PlaySound("start");

        Level1();

        menuStartContainer.Enabled = true; menuStartContainer.Visible = true;
    }

    private void timer1_Tick(object sender, EventArgs e)
    {
        // Movement

        // Z�kladn� hodnoty
        playerLeftOffset = player.Left + 0;
        playerRightOffset = player.Right - 0;
        playerCenterY = player.Top + player.Height / 2;
        facing = "";
        movementSpeedTarget = 0;
        midAir = true;
        closeToBlockLeft = false; closeToBlockRight = false;
        closeToBlockLeftDist = movementSpeed; closeToBlockRightDist = movementSpeed;
        onBlockLeft = false; onBlockRight = false;
        playerBlockHeightDiff = 0;
        slide = false;
        grab = false;

        // Kurzor
        Point cursor = this.PointToClient(Cursor.Position);

        updateCamera();

        // Pohyb
        /// Inputy
        if (leftInput)
        {
            left = true;
            facing = "Left";
            lastStraightFacing = "Left";
        }
        else
            left = false;

        if (rightInput)
        {
            right = true;
            facing = "Right";
            lastStraightFacing = "Right";
        }
        else
            right = false;

        if (upInput)
            facing += "Up";
        else if (downInput)
            facing += "Down";

        if (facing == "")
            facing = lastStraightFacing;

        // Interakce s bloky
        foreach (PictureBox block in gameScreen.Controls.OfType<PictureBox>().Where(block => block.Tag != null))
        {
            if (!block.Tag.ToString().Contains("jump-through"))
            {
                // Postrann� kolize
                if (block.Tag.ToString().Contains("collision") && player.Bounds.IntersectsWith(block.Bounds))
                {
                    if (playerLeftOffset < block.Right && player.Right > block.Left + player.Width / 2 &&
                        player.Bottom > block.Top + 1 && player.Top < block.Bottom)
                    {
                        left = false;
                    }

                    if (playerRightOffset > block.Left && player.Left < block.Right - player.Width / 2 &&
                        player.Bottom > block.Top + 1 && player.Top < block.Bottom)
                    {
                        right = false;
                    }
                }

                // Pokud je hr�� bl�zko k bloku, p�ibl�� se pouze o rozd�l mezi hranou hr��e a bloku (proti bug�m)
                if (block.Tag.ToString().Contains("collision"))
                {
                    if (playerLeftOffset - block.Right < movementSpeedMax && playerLeftOffset - block.Right >= 0 &&
                        player.Bottom > block.Top + 1 && player.Top < block.Bottom)
                    {
                        closeToBlockLeft = true;
                        closeToBlockLeftDist = playerLeftOffset - block.Right;

                        if (playerLeftOffset - block.Right == 0 &&
                            player.Top + player.Height / 2 < block.Bottom)
                        {
                            onBlockLeft = true;
                            midAir = false;
                            playerBlockHeightDiff = player.Bottom - block.Top;
                        }
                    }

                    if (block.Left - playerRightOffset < movementSpeedMax && block.Left - playerRightOffset >= 0 &&
                        player.Bottom > block.Top + 1 && player.Top < block.Bottom)
                    {
                        closeToBlockRight = true;
                        closeToBlockRightDist = block.Left - playerRightOffset;

                        if (block.Left - playerRightOffset == 0 &&
                            player.Top + player.Height / 2 < block.Bottom)
                        {
                            onBlockRight = true;
                            midAir = false;
                            playerBlockHeightDiff = player.Bottom - block.Top;
                        }
                    }

                    // midAir
                    if (block.Top - player.Bottom == -1 &&
                        playerLeftOffset < block.Right && playerRightOffset > block.Left)
                    {
                        midAir = false;
                    }
                }

                // Slide aktivace
                if (((onBlockLeft && leftInput) || (onBlockRight && rightInput)) && force < 0)
                {
                    slide = true;
                    midAir = false;
                }

                // Grab aktivace
                if (grabInput && (onBlockLeft || onBlockRight))
                {
                    grab = true;
                    midAir = false;

                    lastGrabbedOn = onBlockLeft ? "Left" : onBlockRight ? "Right" : "";
                }
            }
        }

        // Hr�� dr�� mezern�k
        if (jumpInput && !midAir && !jumpCooldown && !grabAfterJumpCooldown)
        {
            jump = true;
            force = 15;
            PlaySound("jumped");

            if (onBlockLeft || onBlockRight)
            {
                if (!onBlockDown)
                {
                    movementSpeed = onBlockLeft ? movementSpeedMax * 2 : onBlockRight ? -movementSpeedMax * 2 : 0;

                    force = 11;
                }
            }

            if (slide)
            {
                movementSpeed = onBlockLeft ? movementSpeedMax * 2 : onBlockRight ? -movementSpeedMax * 2 : 0;

                force = 11;
            }

            if (grab)
            {
                if (onBlockLeft && facing.Contains("Right") || onBlockRight && facing.Contains("Left"))
                    force = 13;
                else
                    force = 11;

                movementSpeed += facing.Contains("Left") ? -movementSpeedMax * 2 : facing.Contains("Right") ? movementSpeedMax * 2 : 0;

                grab = false;
                grabAfterJumpCooldown = true;
                timerGrabAfterJumpCooldown.Enabled = true;
            }

            jumpCooldown = true;
            timerJumpCooldown.Enabled = true;
        }

        onBlockDown = false;

        // Grab - V�skok na horn� hran� bloku (<25px spodek hr��e - vr�ek bloku)
        if (grab && !grabAfterJumpCooldown)
        {
            if (playerBlockHeightDiff < 25)
            {
                force = playerBlockHeightDiff / 2;
                climbed = true;

                grab = false;
                grabAfterJumpCooldown = true;
                timerGrabAfterJumpCooldown.Enabled = true;
            }
        }
        if (climbed && !(onBlockLeft || onBlockRight))
        {
            movementSpeed = lastGrabbedOn == "Left" ? -12 : lastGrabbedOn == "Right" ? 12 : 0;
            climbed = false;
            lastGrabbedOn = "";
        }

        // Funkce Dash
        if (dashInput && !dashed)
        {
            switch (facing)
            {
                case "Right":
                    movementSpeed = 4 * movementSpeedMax;
                    force = 0;

                    dashedNonVertical = true;
                    timerDashedNonVertical.Enabled = true;
                    break;

                case "RightUp":
                    movementSpeed = Convert.ToInt32((double)(Math.Sqrt(2) / (double)2) * 4 * movementSpeedMax);
                    force = movementSpeed;

                    dashedNonVertical = true;
                    timerDashedNonVertical.Enabled = true;
                    break;

                case "Up":
                    movementSpeed = 0;
                    force = 22;
                    break;

                case "LeftUp":
                    movementSpeed = Convert.ToInt32((double)(Math.Sqrt(2) / (double)2) * -4 * movementSpeedMax);
                    force = -movementSpeed;

                    dashedNonVertical = true;
                    timerDashedNonVertical.Enabled = true;
                    break;

                case "Left":
                    movementSpeed = -4 * movementSpeedMax;
                    force = 0;

                    dashedNonVertical = true;
                    timerDashedNonVertical.Enabled = true;
                    break;

                case "LeftDown":
                    movementSpeed = Convert.ToInt32((double)(Math.Sqrt(2) / (double)2) * -4 * movementSpeedMax);
                    force = movementSpeed;

                    dashedNonVertical = true;
                    timerDashedNonVertical.Enabled = true;
                    break;

                case "Down":
                    movementSpeed = 0;
                    force = -22;
                    break;

                case "RightDown":
                    movementSpeed = Convert.ToInt32((double)(Math.Sqrt(2) / (double)2) * 4 * movementSpeedMax);
                    force = -movementSpeed;

                    dashedNonVertical = true;
                    timerDashedNonVertical.Enabled = true;
                    break;
            }

            if (Math.Abs(movementSpeed) > movementSpeedMaxTarget)
                movementSpeedMax = Math.Abs(movementSpeed);

            PlaySound("dash");
            dashed = true;
        }

        // Funkce Grab
        if (grab && !grabAfterJumpCooldown && Math.Abs(movementSpeed) < movementSpeedMax)
        {
            PlaySound("grabOn");

            if (!(leftInput || rightInput || upInput || downInput))
                facing = onBlockLeft ? "Left" : onBlockRight ? "Right" : lastStraightFacing;

            if (facing == "Up")
                player.Top -= movementSpeedMax / 5 * 4;

            if (facing == "Down")
                player.Top += movementSpeedMax / 5 * 4;

            movementSpeedTarget = 0;
            force = 0;
        }
        else  // Gravitace
        {
            player.Top -= force;
            if (force > (slide ? -2 : -25) && !dashedNonVertical)
                force -= 1;
        }

        foreach (PictureBox block in gameScreen.Controls.OfType<PictureBox>().Where(block => block.Tag != null))
        {
            // Interakce s bloky
            if (block.Tag.ToString().Contains("collision") && player.Bounds.IntersectsWith(block.Bounds) &&
                playerLeftOffset < block.Right &&
                playerRightOffset > block.Left)
            {
                // Vrchn� kolize
                if ((player.Bottom >= block.Top && player.Top < block.Top)) /// Zeshora
                {
                    if (!block.Tag.ToString().Contains("jump-through"))
                    {
                        player.Top = block.Top - player.Height + 1;
                    }
                    else if (player.Bottom == block.Top)
                    {
                        player.Top = block.Top - player.Height + 1;
                    }

                    force = 0;
                    jump = false;

                    onBlockDown = true;
                    dashed = false;

                    if (!block.Tag.ToString().Contains("spring"))
                    {
                        if (!touchedGround)
                            PlaySound("landed");
                        touchedGround = true;
                    }
                }

                // Spodn� kolize
                if ((player.Top < block.Bottom && player.Bottom > block.Bottom) && !block.Tag.ToString().Contains("jump-through")) /// Zespodu
                {
                    if (force > 3)
                        force = -3;
                    else
                        force *= -1;

                    player.Top = block.Bottom;

                    jumpCooldown = true;
                    timerJumpHeadBumpCooldown.Enabled = true;
                }
            }

            if (block.Tag.ToString().Contains("spring") && player.Bounds.IntersectsWith(block.Bounds) &&
                playerLeftOffset < block.Right &&
                playerRightOffset - 1 > block.Left)
            {
                force = 30;
                jump = true;

                PlaySound("spring");
            }
        }

        // Pohyb do stran
        if (left ^ right && !slide && !grab)
        {
            if (left)
            {
                movementSpeedTarget = -movementSpeedMax;

                if (closeToBlockLeft)
                {
                    movementSpeed = 0;
                    player.Left -= closeToBlockLeftDist;
                }
                else if (movementSpeed != movementSpeedTarget)
                {
                    movementSpeed += movementSpeed < movementSpeedTarget ? 1 : movementSpeed > movementSpeedTarget ? -1 : 0;

                    if (!midAir)
                        movementSpeed = movementSpeedTarget;
                }
            }

            if (right)
            {
                movementSpeedTarget = movementSpeedMax;

                if (closeToBlockRight)
                {
                    movementSpeed = 0;
                    player.Left += closeToBlockRightDist;
                }
                else if (movementSpeed != movementSpeedTarget)
                {
                    movementSpeed += movementSpeed < movementSpeedTarget ? 1 : movementSpeed > movementSpeedTarget ? -1 : 0;

                    if (!midAir)
                        movementSpeed = movementSpeedTarget;
                }
            }
        }
        else
        {
            if (movementSpeed != 0)
            {
                movementSpeedTarget = 0;

                if (movementSpeed < -closeToBlockLeftDist && closeToBlockLeft)
                {
                    movementSpeed = 0;
                    player.Left -= closeToBlockLeftDist;
                }
                else if (movementSpeed > closeToBlockRightDist && closeToBlockRight)
                {
                    movementSpeed = 0;
                    player.Left += closeToBlockRightDist;
                }
                else
                    movementSpeed += movementSpeed < movementSpeedTarget ? 1 : movementSpeed > movementSpeedTarget ? -1 : 0;
            }
        }
        player.Left += movementSpeed;


        // V�voj��sk� statistiky [F3]
        lbDeveloperStats.Text =
            $"Cursor: [{cursor.X}; {cursor.Y}]" +
            $"\r\nPlayer: [{player.Left}; {player.Bottom}]" +
            $"\r\nCameraMovementSpeed: {cameraMovementSpeed}" +
            $"\r\nCameraMovementSpeedTarget: {cameraMovementSpeedTarget}" +
            $"\r\nMovementSpeed: {movementSpeed}" +
            $"\r\nMovementSpeedTarget: {movementSpeedTarget}" +
            $"\r\nForce: {force}" +
            $"\r\nFacing: {facing}" +
            $"\r\nJump: {jump}" +
            $"\r\nJumpInput: {jumpInput}" +
            $"\r\nMidAir: {midAir}" +
            $"\r\nJumpCooldown: {jumpCooldown}" +
            $"\r\nCloseToBlock: {(closeToBlockLeft ? "Left" : closeToBlockRight ? "Right" : "none")}" +
            $"\r\nOnBlock: {(onBlockLeft ? "Left" : onBlockRight ? "Right" : "none")}" +
            $"\r\nOnBlockDown: {onBlockDown}" +
            $"\r\nPlayerBlockHeightDiff: {playerBlockHeightDiff}" +
            $"\r\nGrabInput: {grabInput}" +
            $"\r\nGrab: {grab}" +
            $"\r\nGrabAfterJumpCooldown: {grabAfterJumpCooldown}" +
            $"\r\nLastGrabbedOn: {lastGrabbedOn}" +
            $"\r\nDashInput: {dashInput}" +
            $"\r\nDashed: {dashed}" +
            $"\r\nGameScreen.Top: {gameScreen.Top}";


        if (movementSpeedMax != movementSpeedMaxTarget)
        {
            if (movementSpeedMax > movementSpeedMaxTarget)
                movementSpeedMax--;
            else
                movementSpeedMax = movementSpeedMaxTarget;
        }

        jumpInput = false;
        dashInput = false;

        if (!onBlockDown)
            touchedGround = false;
    }

    #region Kamera

    private void updateCamera()
    {
        cameraMovementSpeedTarget = (432 - playerCenterY) - gameScreen.Top;

        if (gameScreen.Top >= 0 && playerCenterY < 432)
        {
            cameraMovementSpeedTarget = 0;
            cameraMovementSpeed = 0;
        }
        else if (gameScreen.Bottom <= 864 && gameScreen.Height - playerCenterY < 432)
        {
            cameraMovementSpeedTarget = 0;
            cameraMovementSpeed = 0;
        }

        if (cameraMovementSpeed != cameraMovementSpeedTarget)
        {
            if (cameraMovementSpeed < cameraMovementSpeedTarget)
            {
                cameraMovementSpeed += cameraMovementSpeedTarget / 10 + (force > 10 ? (force - 10) : 0) - cameraMovementSpeed;
            }
            else if (cameraMovementSpeed > cameraMovementSpeedTarget)
            {
                cameraMovementSpeed -= cameraMovementSpeed - cameraMovementSpeedTarget / 10 + (force < -10 ? (-force - 10) : 0);
            }
        }

        // Fix na hran� obrazovky proti viditeln�mu zaseknut�
        if (cameraMovementSpeedTarget > 0 && gameScreen.Top >= 0 - cameraMovementSpeed && playerCenterY < 432)
        {
            gameScreen.Top = 0;
        }
        else if (cameraMovementSpeedTarget < 0 && gameScreen.Bottom <= 864 - cameraMovementSpeed && gameScreen.Height - playerCenterY < 432)
        {
            gameScreen.Top = 864 - gameScreen.Height;
        }
        else
        {
            gameScreen.Top += cameraMovementSpeed;
        }
    }

    // Zam��� kameru nahoru
    private void CameraFocus(string focus)
    {
        switch (focus)
        {
            case "Player": gameScreen.Top = 432 - player.Top - player.Height / 2; break;
            case "Top": gameScreen.Top = 0; break;
            case "Bottom": gameScreen.Top = 864 - gameScreen.Height; break;
        }
    }

    #endregion Kamera

    #region Cooldowny

    //// Cooldown 30ms na v�skok po v�skoku
    private void timerJumpCooldown_Tick(object sender, EventArgs e)
    {
        jumpInput = false;
        jumpCooldown = false;
        timerJumpCooldown.Enabled = false;
    }

    //// Cooldown 300ms na skok pokud se bouchne hlavou o spodek bloku
    private void timerJumpHeadBumpCooldown_Tick(object sender, EventArgs e)
    {
        jumpCooldown = false;
        timerJumpHeadBumpCooldown.Enabled = false;
    }

    //// Cooldown 280ms na Grab po v�skoku z Grabu
    private void timerGrabCooldown_Tick(object sender, EventArgs e)
    {
        grabAfterJumpCooldown = false;
        timerGrabAfterJumpCooldown.Enabled = false;
    }

    //// Vypnut� gravitace na 100ms po Dashi (pokud nedashnul vertik�ln�)
    private void timerDashedNonVertical_Tick(object sender, EventArgs e)
    {
        dashedNonVertical = false;
        timerDashedNonVertical.Enabled = false;
    }

    #endregion Cooldowny

    #region Vstupy

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (inputEnabled)
        {
            switch (e.KeyCode)
            {
                case Keys.W:
                    upInput = true;
                    break;

                case Keys.S:
                    downInput = true;
                    break;

                case Keys.A:
                    leftInput = true;
                    break;

                case Keys.D:
                    rightInput = true;
                    break;

                case Keys.Space:
                    if (spaceReleased)
                    {
                        jumpInput = true;
                        spaceReleased = false;
                    }
                    break;

                case Keys.ControlKey:
                    if (ctrlReleased)
                    {
                        dashInput = true;
                        ctrlReleased = false;
                    }
                    break;

                case Keys.ShiftKey:
                    grabInput = true;
                    break;
            }
        }

        switch (e.KeyCode)
        {
            case Keys.Escape:
                if (!menuStartContainer.Enabled)
                {
                    if (menuEscapeContainer.Enabled)   // Pokud je na obrazovce pauza
                    {
                        menuEscapeBtContinue.PerformClick();
                    }
                    else if (menuControlsContainer.Enabled)   // Pokud je na obrazovce ovl�d�n�
                    {
                        menuControlsBtEscapeMenu.PerformClick();
                    }
                    else   // Pokud je ve h�e
                    {
                        menuEscapeContainer.Enabled = true; menuEscapeContainer.Visible = true;
                        gameScreen.Enabled = false; gameScreen.Visible = false;
                        timer1.Enabled = false;
                        inputEnabled = false;
                    }
                }
                break;

            case Keys.NumPad0:
                developerKeys = true;
                break;
        }

        // Funkce pro testov�n�
        if (developerKeys)
        {
            switch (e.KeyCode)
            {
                case Keys.F3:   // Developer stats
                    lbDeveloperStats.Visible = !lbDeveloperStats.Visible;
                    break;

                case Keys.NumPad1:
                    spawnLevel(1);
                    break;

                case Keys.NumPad2:
                    spawnLevel(2);
                    break;
            }
        }
    }

    private void MainWindow_KeyUp(object sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.W:
                upInput = false;
                break;

            case Keys.S:
                downInput = false;
                break;

            case Keys.A:
                leftInput = false;
                break;

            case Keys.D:
                rightInput = false;
                break;

            case Keys.Space:
                spaceReleased = true;
                break;

            case Keys.ControlKey:
                ctrlReleased = true;
                break;

            case Keys.ShiftKey:
                grabInput = false;
                PlaySound("grabOff");
                break;

            case Keys.NumPad0:
                developerKeys = false;
                break;
        }
    }

    private void buttonClicked(object sender, EventArgs e)
    {
        Button clickedButton = sender as Button;

        switch (clickedButton.Name)
        {
            case "startBtPlay":    // Zapnut� hry ze Start menu
                movementSpeed = 0; force = 0;
                spawnLevel(1);

                menuStartContainer.Enabled = false;
                menuStartContainer.Visible = false;
                gameScreen.Enabled = true;
                gameScreen.Visible = true;
                timer1.Enabled = true;
                inputEnabled = true;
                break;

            case "startBtClose":    // Vypnut� hry ze Start menu
                Close();
                break;

            case "menuEscapeBtContinue":    // Pokra�ov�n� ve h�e z Escape menu
                menuEscapeContinue(false);
                break;

            case "menuEscapeBtResetScreen":    // Reset obrazovky z Escape menu
                menuEscapeContinue(true);
                break;

            case "menuEscapeBtControls":    // Zobrazen� ovl�d�n� v Escape menu
                menuEscapeContainer.Enabled = false; menuEscapeContainer.Visible = false;
                menuControlsContainer.Enabled = true; menuControlsContainer.Visible = true;
                break;

            case "menuControlsBtEscapeMenu":    // Odchod do Escape menu ze zobrazen� ovl�d�n�
                menuControlsContainer.Enabled = false; menuControlsContainer.Visible = false;
                menuEscapeContainer.Enabled = true; menuEscapeContainer.Visible = true;
                break;

            case "menuEscapeBtStartMenu":    // Odchod do Start menu z Escape menu
                menuControlsContainer.Enabled = false; menuControlsContainer.Visible = false;
                menuEscapeContainer.Enabled = false; menuEscapeContainer.Visible = false;

                timer1.Enabled = false;
                inputEnabled = false;

                gameScreen.Enabled = false; gameScreen.Visible = false;
                menuStartContainer.Enabled = true;
                menuStartContainer.Visible = true;
                break;
        }

        Focus();
    }

    private void menuEscapeContinue(bool restart)
    {
        if (restart)
        {
            movementSpeed = 0; force = 0;
            spawnLevel(currentLevel);
        }

        menuControlsContainer.Enabled = false; menuControlsContainer.Visible = false;
        menuEscapeContainer.Enabled = false; menuEscapeContainer.Visible = false;
        gameScreen.Enabled = true; gameScreen.Visible = true;
        timer1.Enabled = true;
        inputEnabled = true;
    }

    #endregion Vstupy

    #region Level design

    private void Level1()
    {
        gameScreen.Height = 864;

        Terrain pictureBox1 = new(0, 768, 1339, 96, "collision", Color.FromArgb(72, 55, 34), false, Resources.blank, gameScreen);
        Terrain pictureBox2 = new(337, 717, 77, 51, "collision", Color.FromArgb(72, 55, 34), false, Resources.blank, gameScreen);
        Terrain pictureBox3 = new(645, 684, 222, 84, "collision", Color.FromArgb(72, 55, 34), false, Resources.blank, gameScreen);
        Terrain pictureBox4 = new(942, 609, 190, 84, "collision", Color.FromArgb(72, 55, 34), false, Resources.blank, gameScreen);
        Terrain pictureBox5 = new(281, 314, 66, 113, "collision", Color.FromArgb(115, 149, 218), false, Resources.blank, gameScreen);
        Terrain pictureBox6 = new(355, 697, 51, 20, "collision spring", Color.FromArgb(154, 205, 50), false, Resources.blank, gameScreen);
        Terrain pictureBox7 = new(67, 549, 66, 113, "collision", Color.FromArgb(115, 149, 218), false, Resources.blank, gameScreen);
        Terrain pictureBox8 = new(77, 133, 66, 113, "collision", Color.FromArgb(115, 149, 218), false, Resources.blank, gameScreen);
        Terrain pictureBox9 = new(507, 423, 222, 62, "collision", Color.FromArgb(115, 149, 218), false, Resources.blank, gameScreen);
        Terrain pictureBox10 = new(1256, 549, 259, 56, "collision", Color.FromArgb(72, 55, 34), false, Resources.blank, gameScreen);
        Terrain pictureBox11 = new(942, 279, 222, 84, "collision", Color.FromArgb(115, 149, 218), false, Resources.blank, gameScreen);
        Terrain pictureBox12 = new(470, 111, 397, 55, "collision", Color.FromArgb(72, 55, 34), false, Resources.blank, gameScreen);

        terrainArray = new Terrain[] { pictureBox1, pictureBox2, pictureBox3, pictureBox4, pictureBox5, pictureBox6, pictureBox7, pictureBox8, pictureBox9, pictureBox10, pictureBox11, pictureBox12, };

        player.Left = 186;
        player.Top = 701;

        CameraFocus("Bottom");
    }

    private void Level2()
    {
        gameScreen.Height = 1686;

        Terrain pictureBox1 = new(0, 1590, 1339, 96, "collision", Color.FromArgb(95, 68, 35), false, Resources.blank, gameScreen);
        Terrain pictureBox2 = new(219, 1547, 235, 43, "collision", Color.FromArgb(95, 68, 35), false, Resources.blank, gameScreen);
        Terrain pictureBox3 = new(645, 1506, 222, 84, "collision", Color.FromArgb(95, 68, 35), false, Resources.blank, gameScreen);
        Terrain pictureBox4 = new(942, 1431, 190, 84, "collision", Color.FromArgb(95, 68, 35), false, Resources.blank, gameScreen);
        Terrain pictureBox5 = new(271, 1245, 66, 113, "collision", Color.FromArgb(115, 149, 218), false, Resources.blank, gameScreen);
        Terrain pictureBox6 = new(1230, 1570, 51, 20, "collision spring", Color.FromArgb(154, 205, 50), false, Resources.blank, gameScreen);
        Terrain pictureBox7 = new(31, 1164, 66, 426, "collision", Color.FromArgb(115, 149, 218), false, Resources.blank, gameScreen);
        Terrain pictureBox8 = new(305, 461, 66, 540, "collision", Color.FromArgb(115, 149, 218), false, Resources.blank, gameScreen);
        Terrain pictureBox9 = new(507, 1245, 222, 62, "collision", Color.FromArgb(115, 149, 218), false, Resources.blank, gameScreen);
        Terrain pictureBox10 = new(1330, 169, 149, 790, "collision", Color.FromArgb(95, 68, 35), false, Resources.blank, gameScreen);
        Terrain pictureBox11 = new(942, 1101, 222, 84, "collision", Color.FromArgb(115, 149, 218), false, Resources.blank, gameScreen);
        Terrain pictureBox12 = new(470, 933, 397, 55, "collision", Color.FromArgb(95, 68, 35), false, Resources.blank, gameScreen);
        Terrain pictureBox13 = new(793, 913, 51, 20, "collision spring", Color.FromArgb(154, 205, 50), false, Resources.blank, gameScreen);
        Terrain pictureBox14 = new(1016, 790, 190, 48, "collision", Color.FromArgb(95, 68, 35), false, Resources.blank, gameScreen);
        Terrain pictureBox15 = new(371, 726, 180, 30, "collision jump-through", Color.FromArgb(65, 50, 31), false, Resources.blank, gameScreen);
        Terrain pictureBox16 = new(31, 31, 200, 336, "collision", Color.FromArgb(115, 149, 218), false, Resources.blank, gameScreen);
        Terrain pictureBox17 = new(470, 240, 228, 48, "collision", Color.FromArgb(95, 68, 35), false, Resources.blank, gameScreen);
        Terrain pictureBox18 = new(1104, 269, 226, 30, "collision jump-through", Color.FromArgb(65, 50, 31), false, Resources.blank, gameScreen);

        terrainArray = new Terrain[] { pictureBox1, pictureBox2, pictureBox3, pictureBox4, pictureBox5, pictureBox6, pictureBox7, pictureBox8, pictureBox9, pictureBox10, pictureBox11, pictureBox12, pictureBox13, pictureBox14, pictureBox15, pictureBox16, pictureBox17, pictureBox18, };

        player.Left = 647;
        player.Top = 866;

        CameraFocus("Player");
    }

    private void spawnLevel(int level)
    {
        foreach (Terrain terrain in terrainArray)
        {
            DestroyAll(terrain.pb, gameScreen);
        }

        switch (level)
        {
            case 1: Level1(); break;
            case 2: Level2(); break;
        }

        currentLevel = level;
    }

    private void DestroyAll(PictureBox pb, Panel panel)
    {
        pb.Bounds = Rectangle.Empty;
        panel.Controls.Remove(pb);
        pb.Dispose();
    }

    #endregion Level design

    #region Sound design

    // Cesty soubor� - !!! P�ED PUBLIKAC� ODSTRANIT '../../../'
    string filePathSpring = "../../../sounds/spring.wav";
    string filePathGrabOn = "../../../sounds/wow_so_secret.wav";
    string filePathGrabOff = "../../../sounds/spring.wav";

    private WaveOutEvent waveOutSpring;
    private WaveOutEvent waveOutGrabOn;
    private WaveOutEvent waveOutGrabOff;

    private void PlaySound(string sound)
    {
        // Jumped
        if (sound == "jumped" || sound == "start")
        {

        }

        // Landed
        if (sound == "landed" || sound == "start")
        {

        }

        // Spring
        if (sound == "spring" || sound == "start")
        {
            if (sound != "start")
            {
                // Zahozen� dohran�ho zvuku
                waveOutSpring.Stop();
                waveOutSpring.Dispose();
            }

            // Na�ten� audio souboru ze soubor� hry
            AudioFileReader audioFileSpring = new AudioFileReader(filePathSpring);

            // Nov� instance AudioFileReaderu a WaveOutEventu
            waveOutSpring = new WaveOutEvent();

            // P�id�n� AudioFileReaderu do v�stupu
            waveOutSpring.Init(audioFileSpring);

            // P�ehr�n� zvuku
            if (sound != "start")
                waveOutSpring.Play();
        }

        // GrabOn
        if (sound == "grabOn" || sound == "start")
        {
            if (sound != "start")
            {
                waveOutGrabOn.Stop();
                waveOutGrabOn.Dispose();
            }
            AudioFileReader audioFileGrabOn = new AudioFileReader(filePathGrabOn);
            waveOutGrabOn = new WaveOutEvent();
            waveOutGrabOn.Init(audioFileGrabOn);

            if (sound != "start")
            {
                if (!grabbedOn)
                    waveOutGrabOn.Play();

                grabbedOn = true;
            }
        }

        // GrabOff
        if (sound == "grabOff" || sound == "start")
        {
            if (sound != "start")
            {
                waveOutGrabOff.Stop();
                waveOutGrabOff.Dispose();
            }
            AudioFileReader audioFileGrabOff = new AudioFileReader(filePathGrabOff);
            waveOutGrabOff = new WaveOutEvent();
            waveOutGrabOff.Init(audioFileGrabOff);

            if (sound != "start")
            {
                if (grabbedOn)
                    waveOutGrabOff.Play();
                grabbedOn = false;
            }
        }

        // Dash
        if (sound == "dash" || sound == "start")
        {

        }
    }

    #endregion Sound design
}