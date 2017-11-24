using DigitalPlatform;
using DigitalPlatform.LibraryClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using System.Xml;

namespace demo
{
    public partial class Form_Main : Form
    {
        public Form_Main()
        {
            InitializeComponent();
        }

        // 窗体加载
        private void Form_Main_Load(object sender, EventArgs e)
        {
            string dp2ServerUrl = Properties.Settings.Default.dp2ServerUrl;
            if (string.IsNullOrEmpty(dp2ServerUrl))
            {
                Form_Setting dlg = new Form_Setting();
                dlg.ShowDialog(this);
            }
        }

        //服务器配置
        private void dp2服务器配置SToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form_Setting dlg = new Form_Setting();
            dlg.ShowDialog(this);
        }

        //分析
        private void button_analysis_Click(object sender, EventArgs e)
        {
            // 先加载模块文件
            string appDir = Application.StartupPath;
            string filePath = appDir + "\\" + "analysisTemplate.txt";
            if (File.Exists(filePath) == false)
            {
                MessageBox.Show("未配置模板文件analysisTemplate.txt");
                return;
            }
            StreamReader s = new StreamReader(filePath,Encoding.UTF8);
            string html = s.ReadToEnd();

            // 先获取读者信息
            string patronBarcode = this.textBox_patron.Text.Trim();
            Patron patron = null;
            string error = "";
            int nRet = dp2analysisService.Instance.GetPatronInfo(patronBarcode,
                out patron,
                out error);
            if (nRet == -1)
            {
                MessageBox.Show(this, error);
                return;
            }

            // 替换读者信息
            html = html.Replace("%name%", patron.name);
            html = html.Replace("%gender%", patron.gender);
            html = html.Replace("%department%", patron.department);

            html = html.Replace("%firstCheckoutDate%", patron.firstBorrowDate);
            html = html.Replace("%checkoutCount%", patron.historyCount .ToString());

            html = html.Replace("%latestCheckoutTable%", patron.historyTable);

            // 按分类统计数量
            html = html.Replace("%covertClcCount%", patron.covertClcCount.ToString());
            html = html.Replace("%clcTable%", patron.clcTable);

            // 按年份统计数量
            html = html.Replace("%yearTable%", patron.yearTable);

            SetHtmlString(this.webBrowser1, html);
        }


 

        // 给浏览器控件设置html
        public static void SetHtmlString(WebBrowser webBrowser,
            string strHtml)
        {
            webBrowser.DocumentText = strHtml;
        }

        // 设置文本
        static void SetTextString(WebBrowser webBrowser, string strText)
        {
            SetHtmlString(webBrowser, "<pre>" + HttpUtility.HtmlEncode(strText) + "</pre>");
        }
    }
}
