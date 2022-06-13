using DigitalPlatform;
using DigitalPlatform.LibraryClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace demo
{
    public partial class Form_Setting : Form
    {



        public Form_Setting()
        {
            InitializeComponent();

        }




        public string dp2ServerUrl
        {
            get
            {
                return this.textBox_dp2serverUrl.Text;
            }
            set
            {
                this.textBox_dp2serverUrl.Text = value;
            }
        }

        public string dp2Username
        {
            get
            {
                return this.textBox_dp2username.Text;
            }

            set
            {
                this.textBox_dp2username.Text = value;
            }
        }

        public string dp2Password
        {
            get
            {
                return this.textBox_dp2password.Text;
            }

            set
            {
                this.textBox_dp2password.Text = value;
            }
        }

        private void Form_CreateTestEnv_Load(object sender, EventArgs e)
        {
            this.dp2ServerUrl = Properties.Settings.Default.dp2ServerUrl;
            this.dp2Username = Properties.Settings.Default.dp2Username;
            this.dp2Password = Properties.Settings.Default.dp2Password;
        }

        private void button_verify_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.dp2ServerUrl = this.dp2ServerUrl;
            Properties.Settings.Default.dp2Username = this.dp2Username;
            Properties.Settings.Default.dp2Password = this.dp2Password;

            string error = "";
            int nRet = dp2analysisService.Instance.Verify(this.dp2Username,
                this.dp2Password, 
                out error);
            if (nRet == -1 || nRet == 0)
            {
                MessageBox.Show(this, "出错:["+error+"]");
                return;
            }

            MessageBox.Show(this, "帐户已存在");
            return;

        }
 

        private void button_cancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void button_ok_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.dp2ServerUrl =this.dp2ServerUrl ;
            Properties.Settings.Default.dp2Username = this.dp2Username;
            Properties.Settings.Default.dp2Password = this.dp2Password;

            Properties.Settings.Default.Save();

            dp2analysisService.Instance.dp2ServerUrl=this.dp2ServerUrl ;
            dp2analysisService.Instance.dp2Username=this.dp2Username ;
            dp2analysisService.Instance.dp2Password=this.dp2Password ;  
            this.DialogResult = DialogResult.OK;
            this.Close();
        }




    }
}
