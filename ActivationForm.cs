using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArkaiosDJAssistant
{
    public class ActivationForm : Form
    {
        private TextBox txtKey;
        private bool isActivated = false;
        
        public bool IsActivated { get { return isActivated; } }

        public ActivationForm()
        {
            this.Text = "Arkaios DJ Nexus - Activación de Producto";
            this.Size = new Size(500, 360);
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            Label lblTitle = new Label();
            lblTitle.Text = "ESTE PRODUCTO REQUIERE UNA LICENCIA";
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            lblTitle.Height = 50;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblTitle.ForeColor = Color.LightCoral;
            this.Controls.Add(lblTitle);

            Label lblHwid = new Label();
            lblHwid.Text = "Tu Hardware ID: " + LicenseManager.GetHardwareId();
            lblHwid.Location = new Point(30, 60);
            lblHwid.AutoSize = true;
            lblHwid.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            lblHwid.ForeColor = Color.Cyan;
            this.Controls.Add(lblHwid);

            LinkLabel lnkRegister = new LinkLabel();
            lnkRegister.Text = "¿No tienes clave? Regístrate en Arkaios-Expo para obtener tu serial";
            lnkRegister.Location = new Point(30, 90);
            lnkRegister.AutoSize = true;
            lnkRegister.Font = new Font("Segoe UI", 9);
            lnkRegister.LinkColor = Color.DeepSkyBlue;
            lnkRegister.VisitedLinkColor = Color.DeepSkyBlue;
            lnkRegister.Links.Add(lnkRegister.Text.IndexOf("Arkaios-Expo"), "Arkaios-Expo".Length, "https://arkaios-world.web.app/");
            lnkRegister.LinkClicked += (s, ev) => System.Diagnostics.Process.Start(ev.Link.LinkData.ToString());
            this.Controls.Add(lnkRegister);

            LinkLabel lnkPortal = new LinkLabel();
            lnkPortal.Text = "Estado del servicio: servidor-arkaios-api.vercel.app";
            lnkPortal.Location = new Point(30, 112);
            lnkPortal.AutoSize = true;
            lnkPortal.Font = new Font("Segoe UI", 8);
            lnkPortal.LinkColor = Color.DeepSkyBlue;
            lnkPortal.VisitedLinkColor = Color.DeepSkyBlue;
            lnkPortal.Links.Add(lnkPortal.Text.IndexOf("servidor-arkaios-api.vercel.app"), "servidor-arkaios-api.vercel.app".Length, "https://servidor-arkaios-api.vercel.app/");
            lnkPortal.LinkClicked += (s, ev) => System.Diagnostics.Process.Start(ev.Link.LinkData.ToString());
            this.Controls.Add(lnkPortal);

            Label lblInst = new Label();
            lblInst.Text = "Ya con tu clave en mano, pégala aquí:";
            lblInst.Location = new Point(30, 138);
            lblInst.AutoSize = true;
            lblInst.Font = new Font("Segoe UI", 10);
            this.Controls.Add(lblInst);

            txtKey = new TextBox();
            txtKey.Location = new Point(30, 165);
            txtKey.Width = 420;
            txtKey.Height = 80;
            txtKey.Multiline = true;
            txtKey.Font = new Font("Consolas", 9);
            txtKey.BackColor = Color.FromArgb(40, 40, 40);
            txtKey.ForeColor = Color.LightGreen;
            this.Controls.Add(txtKey);

            Button btnActivate = new Button();
            btnActivate.Text = "Activar Software";
            btnActivate.Location = new Point(175, 255);
            btnActivate.Width = 150;
            btnActivate.Height = 35;
            btnActivate.FlatStyle = FlatStyle.Flat;
            btnActivate.BackColor = Color.FromArgb(50, 150, 50);
            btnActivate.ForeColor = Color.White;
            btnActivate.Cursor = Cursors.Hand;
            btnActivate.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnActivate.Click += new EventHandler(BtnActivate_Click);
            this.Controls.Add(btnActivate);
        }

        private void BtnActivate_Click(object sender, EventArgs e)
        {
            string key = txtKey.Text.Trim();
            if (LicenseManager.ValidateKeyLocally(key))
            {
                LicenseManager.SaveLicense(key);
                MessageBox.Show("¡Licencia válida! El software ha sido activado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                isActivated = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Licencia inválida. Verifica que la clave esté completa o que corresponda a este Hardware ID.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
