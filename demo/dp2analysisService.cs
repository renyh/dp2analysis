using DigitalPlatform;
using DigitalPlatform.LibraryClient;
using DigitalPlatform.LibraryClient.localhost;
using DigitalPlatform.Xml;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using DigitalPlatform.Marc;

namespace demo
{
    public class dp2analysisService
    {
       #region 单一实例

        static dp2analysisService _instance;

        // 构造函数
        private dp2analysisService()
        {
            this.dp2ServerUrl = Properties.Settings.Default.dp2ServerUrl;
            this.dp2Username = Properties.Settings.Default.dp2Username;
            this.dp2Password = Properties.Settings.Default.dp2Password;
            this._libraryChannelPool.BeforeLogin += new BeforeLoginEventHandle(_channelPool_BeforeLogin);
        }
        private static object _lock = new object();
        static public dp2analysisService Instance
        {
            get
            {
                if (null == _instance)
                {
                    lock (_lock)  //线程安全的
                    {
                        _instance = new dp2analysisService();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region 关于通道
        internal LibraryChannelPool _libraryChannelPool = new LibraryChannelPool();
     
        public string dp2ServerUrl { get; set; }
        public string dp2Username{get;set;}
        public string dp2Password { get; set; }


        void _channelPool_BeforeLogin(object sender, BeforeLoginEventArgs e)
        {
            
            if (string.IsNullOrEmpty(this.dp2Username))
            {
                e.Cancel = true;
                e.ErrorInfo = "尚未登录";
            }

            e.LibraryServerUrl = this.dp2ServerUrl;
            e.UserName = this.dp2Username;
            e.Parameters = "type=worker,client=dp2analysis|0.01";
            e.Password = this.dp2Password;
            e.SavePasswordLong = true;
        }

        public void ReturnChannel(LibraryChannel channel)
        {
            this._libraryChannelPool.ReturnChannel(channel);
        }

        #endregion

        #region 检查dp2帐户是否存在

        //检查帐户是否存在
        public int Verify(string userName,string passord,out string error)
        {
            error = "";

            LibraryChannel channel = this._libraryChannelPool.GetChannel(this.dp2ServerUrl,
                userName);
            try
            {
                string strError = "";
                long lRet = channel.Login(userName,
                passord,
                "type=worker,client=dp2analysis|0.01",
                out strError);
                if (lRet == -1)
                {
                    return -1;
                }
                else if (lRet == 0)
                {
                    error = "用户名或者密码不存在";
                    return 0;
                }


                return 1;

            }
            finally
            {
                this.ReturnChannel(channel);
            }
        }

        #endregion


        public int GetPatronInfo(string patronBarcode,
            out Patron patron,
            out string error)
        {
            long lRet = 0;
            patron = null;
            error = "";

            LibraryChannel channel = this._libraryChannelPool.GetChannel(this.dp2ServerUrl,
                this.dp2ServerUrl);
            try
            {

                string[] results = null;
                lRet = channel.GetReaderInfo(//null,
                    patronBarcode, //读者卡号,
                    "advancexml",
                    out results,
                    out error);
                if (lRet <= -1)
                {
                    error = "查询读者信息失败：" + error;
                    goto ERROR1;
                }
                else if (lRet == 0)
                {
                    error = "查无此证";
                    goto ERROR1;
                }
                else if (lRet > 1)
                {
                    error = "证号重复";
                    goto ERROR1;
                }

                XmlDocument dom = new XmlDocument();
                string strReaderXml = results[0];
                try
                {
                    dom.LoadXml(strReaderXml);
                }
                catch (Exception ex)
                {
                    error = "读者信息解析错误：" + ExceptionUtil.GetDebugText(ex);
                    goto ERROR1;
                }

                //<name>王一诺</name> 
                //<department>1501</department> 
                //<gender>男</gender>
                XmlNode root = dom.DocumentElement;
                patron = new Patron();
                patron.name = DomUtil.GetElementText(root, "name");
                patron.department = DomUtil.GetElementText(root, "department");
                patron.gender = DomUtil.GetElementText(root, "gender");

                int nRowIndex = 0;
                string myinfo = "";

                // 查询借阅历史
                ChargingHistoryLoader history_loader = new ChargingHistoryLoader();
                history_loader.Channel = channel;
                //history_loader.Stop = this.stop;
                history_loader.PatronBarcode = patronBarcode;
                history_loader.TimeRange = "~"; // strTimeRange;
                history_loader.Actions = "return,lost";
                history_loader.Order = "descending";

                CacheableBiblioLoader summary_loader = new CacheableBiblioLoader();
                summary_loader.Channel = channel;
                //summary_loader.Stop = this.stop;
                summary_loader.Format = "summary";
                summary_loader.GetBiblioInfoStyle = GetBiblioInfoStyle.None;
                // 输出借阅历史
                OutputBorrowHistory(channel,
                    dom,
                    history_loader,
                    summary_loader,
                    ref nRowIndex,
                    ref patron);

                return 1;
            }
            finally
            {
                this.ReturnChannel(channel);

            }

        ERROR1:
            LogManager.Logger.Error(error);
            return -1;

        }


        // parameters:
        //      bAdvanceXml 是否为 AdvanceXml 情况
        static void OutputBorrowHistory(LibraryChannel channel,
            XmlDocument reader_dom,
            ChargingHistoryLoader history_loader,
            CacheableBiblioLoader summary_loader,
            ref int nRowIndex,
            ref Patron patron)
        {
            int nStartRow = nRowIndex;

            // 第一笔借书的时间
            string firstBorrowDate = "";


            string history= "<table>"
                + "<tr>"
                + "<td>序号</td><td>借书日期</td><td>册条码号</td>"
                + "<td>书刊名称</td><td>索取号</td>"
                + "</tr>";

            Hashtable clcHash= new Hashtable();
            Hashtable yearHash = new Hashtable();
            
            int nItemIndex = 0;
            foreach (ChargingItemWrapper wrapper in history_loader)
            {
                ChargingItem item = wrapper.Item;
                ChargingItem rel = wrapper.RelatedItem;

                string strItemBarcode = item.ItemBarcode;
                string strBorrowDate = rel == null ? "" : rel.OperTime;
                if (strBorrowDate.Length > 10)
                    strBorrowDate = strBorrowDate.Substring(0, 10);

                // ==加到每年借书数量hashtable
                if (strBorrowDate.Length > 4)
                {
                    string year = strBorrowDate.Substring(0, 4);
                    int yearCouter = 0;
                    if (yearHash.ContainsKey(year) == true)
                    {
                        yearCouter = (int)yearHash[year];
                    }
                    yearCouter++;
                    yearHash[year] = yearCouter;
                }


                // ==加入书目摘要，一次一条记录
                string strSummary = "";
                List<string> item_barcodes = new List<string>();
                item_barcodes.Add("@itemBarcode:" + strItemBarcode);
                summary_loader.RecPaths = item_barcodes;
                foreach (BiblioItem biblio in summary_loader)
                {
                    strSummary = biblio.Content;
                    strSummary = GetShortSummary(strSummary);
                }


                // ==获取索取号
                string accessNo = "";

                // 获取册记录
                string strItemXml = "";
                string strBiblio = "";
                string strError = "";
                long lRet = channel.GetItemInfo(//null,
                    strItemBarcode,
                    "xml",
                    out strItemXml,
                    "xml",
                    out strBiblio,
                    out strError);
                if (-1 >= lRet)
                {
                    accessNo = "获得'" + strItemBarcode + "'发生错误: " + strError;
                }
                else if (0 == lRet)
                {
                    accessNo = strItemBarcode + " 记录不存在";
                }
                else if (1 < lRet)
                {
                    accessNo = strItemBarcode + " 记录重复，需馆员处理";
                }
                else
                {
                    // 获取索取号
                    XmlDocument itemDom = new XmlDocument();
                    try
                    {
                        itemDom.LoadXml(strItemXml);
                        //accessNo
                        accessNo = DomUtil.GetElementInnerText(itemDom.DocumentElement, "accessNo");

                        string bigClass = "";
                        if (string.IsNullOrEmpty(accessNo) == true)
                        {
                            bigClass = "[空]";
                        }
                        else
                        {
                            bigClass = accessNo.Substring(0, 1);
                        }

                        int value = 0;
                        if (clcHash.ContainsKey(bigClass) == true)
                        {
                            value = (int)clcHash[bigClass];
                        }
                        value++;
                        clcHash[bigClass] = value;
                    }
                    catch (Exception ex)
                    {
                        accessNo = strItemBarcode + " 加载到dom出错：" + ex.Message;
                    }
                }


                nItemIndex++;

                string uiClass = "";
                if (nItemIndex % 2 == 1)
                {
                    uiClass = " class='grayline' ";
                }


                history += "<tr "+uiClass+">"
                + "<td>" + nItemIndex + "</td><td>" + strBorrowDate + "</td><td>" + strItemBarcode + "</td>"
                + "<td>" + strSummary + "</td><td>" + accessNo + "</td>"
                + "</tr>";

                // 由于是倒序，集合中最后一笔记录是第1次的借书记录
                firstBorrowDate =strBorrowDate;
            }

            history += "</table>";

            // 借书历史
            patron.historyTable = history;
            patron.firstBorrowDate = firstBorrowDate;

            // ==每个类别数量==
            // 先借助ArrayList排序一下
            List<string> list = new List<string>();
            foreach (System.Collections.DictionaryEntry item in clcHash)
            {
                list.Add(item.Key.ToString());
            }
            list.Sort();
            string classTable = "<table class='clcTable'><tr><td>图书种类</td><td>借阅数量</td></tr>";
            foreach (string k in list)
            {
                classTable += "<tr>"
                    + "<td>" + k + "</td><td>" + (int)clcHash[k] + "</td>"
                    + "</tr>";
            }
            classTable += "</table>";
            patron.clcTable = classTable;
            patron.covertClcCount = list.Count;

            //==每年借书数量==
            ArrayList al = new ArrayList(yearHash.Keys);
            al.Sort();
            string yearTable = "<table class='yearTable'><tr><td>借阅时段</td><td>借阅数量</td></tr>";
            foreach (string k in al)
            {
                yearTable += "<tr>"
                    + "<td>" + k + "</td><td>" + (int)yearHash[k] + "</td>"
                    + "</tr>";
            }
            yearTable += "</table>";
            patron.yearTable = yearTable;
        }

        static string GetShortSummary(string summary)
        {
            int nIndex = summary.IndexOf("/");
            if (nIndex > 0)
            {
                summary = summary.Substring(0, nIndex);
            }
            else
            {
                //西文标点，点、空、横、横、空
                nIndex = summary.IndexOf(". -- ");
                if (nIndex > 0)
                {
                    summary = summary.Substring(0, nIndex);
                }
            }
            return summary;
        }



    }

    public class BiblioInfo
    {
        public string Title { get; set; }

        public string Class { get; set; }
    }

}
