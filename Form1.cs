using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebFilesDownload
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if(dialog.ShowDialog() == DialogResult.OK)
            {
                lbl_path.Text = dialog.SelectedPath;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if(textBox1.Text == "" || lbl_path.Text == "")
            {
                MessageBox.Show("请设置web url和save path!");
                return;
            }
            Action<string, string> act = BeginDownload;
            act.BeginInvoke(textBox1.Text, lbl_path.Text, null, null);
        }

        private void BeginDownload(string webUrl, string savePath)
        {
            
            string baseUrl = GetBaseUrl(webUrl);
            string currentUrl = GetCurrentUrl(webUrl);
            string fileName = GetFileName(webUrl);
            string content = new HttpHelper().HttpGetResponse(webUrl);
            if(content == null)
            {
                MessageBox.Show("获取远程页面失败！");
                return;
            }
            if(Directory.Exists(savePath) == false)
            {
                Directory.CreateDirectory(savePath);
            }
            string file = savePath + "\\" + fileName;
            File.WriteAllText(file, content);
            //1、下载基本的文件
            List<string> cssFiles = null;
            List<string> jsFiles = null;
            DownloadBaseFiles(content, baseUrl, currentUrl, savePath, out cssFiles, out jsFiles);
            //2、下载css样式文件里面的图片
            DownloadCSSFiles(content, baseUrl, currentUrl, savePath, cssFiles, jsFiles);
            SetStatus("处理完毕！");
        }

        private void DownloadCSSFiles(string c, string baseUrl, string currentUrl, string savePath, List<string> cssFiles, List<string> jsFiles)
        {
            Dictionary<string, string> dics = new Dictionary<string, string>();
            Regex reg_file = new Regex(@"\((\/|\w|\-|\d|\.)*\.(jpg|png|gif)+\)");
            dics.Add(currentUrl, c);
            foreach (string cssfile in cssFiles)
            {
                string filecontent = File.ReadAllText(cssfile);
                string rpath = currentUrl + cssfile.Replace(savePath, "").Replace("\\", "/");
                dics.Add(GetCurrentUrl(rpath), filecontent);
            }
            foreach (string jsfile in jsFiles)
            {
                string filecontent = File.ReadAllText(jsfile);
                dics.Add(currentUrl + "|js-js|" + UserMd5(filecontent), filecontent);
            }
            foreach (var item in dics)
            {
                string path = item.Key;
                string content = item.Value;
                if(path.IndexOf("|js-js|") > -1)
                {
                    reg_file = new Regex(@"(\/|\w|\-|\d|\.)*\.(jpg|png|gif)+");
                    path = path.Substring(0, path.IndexOf("|js-js|"));
                }
                else
                {
                    reg_file = new Regex(@"\((\/|\w|\-|\d|\.)*\.(jpg|png|gif)+\)");
                }
                if (reg_file.IsMatch(content))
                {
                    foreach (var m in reg_file.Matches(content))
                    {
                        string file = m.ToString().Replace("(", "").Replace(")", "");
                        string localDir = "";
                        string remoteFile = "";
                        string localFile = "";
                        string currentfilepath = GetCurrentUrl(file);
                        string fname = GetFileName(file);
                        if (file.StartsWith("/"))
                        {
                            remoteFile = baseUrl + file;
                            localFile = savePath + (currentfilepath != "" ? currentfilepath + "\\" : "") + fname;
                            localDir = savePath + GetCurrentUrl(file);
                        }
                        else
                        {
                            remoteFile = path + "/" + file;
                            string sp = path == currentUrl ? savePath : savePath + path.Replace(currentUrl, "").Replace("/", "\\");
                            localFile = sp + "\\" + (currentfilepath != "" ? currentfilepath + "\\" : "") + fname.Replace("/", "\\");
                            localDir = sp + (currentfilepath != "" ? "\\" + currentfilepath : "");
                        }
                        if (!Directory.Exists(localDir))
                        {
                            Directory.CreateDirectory(localDir);
                        }
                        if (File.Exists(localFile))
                        {
                            continue;
                        }
                        new HttpHelper().DownLoad(remoteFile, localFile);
                    }
                }
            }
        }

        private void DownloadBaseFiles(string content, string baseUrl, string currentUrl, string savePath, 
            out List<string> cssFiles, out List<string> jsFiles)
        {
            cssFiles = new List<string>();
            jsFiles = new List<string>();
            Regex reg = new Regex("\\\"(\\/|\\w|\\-|\\d|\\.)*\\.(js|jpg|png|css|gif)+");
            if (reg.IsMatch(content))
            {
                MatchCollection ms = reg.Matches(content);
                foreach (var url in ms)
                {
                    string path = url.ToString().TrimStart('"');
                    if (path.IndexOf(" ") > -1)
                    {
                        continue;
                    }
                    string filecurrentpath = GetCurrentUrl(path);
                    string middlePath = filecurrentpath == "" ? "" : filecurrentpath + "/";
                    if (!path.StartsWith("http"))
                    {
                        string currentSavePath = "";
                        string remoteFile = "";
                        if (path.StartsWith("/"))
                        {
                            currentSavePath = savePath + (filecurrentpath != "" ? "\\" + filecurrentpath.Replace("/", "\\") : "");
                            remoteFile = baseUrl + "/" + middlePath + GetFileName(path);
                        }
                        else
                        {
                            currentSavePath = savePath + (filecurrentpath != "" ? "\\" + filecurrentpath.Replace("/", "\\") : "");
                            remoteFile = currentUrl + "/" + middlePath + GetFileName(path);
                        }
                        if (Directory.Exists(currentSavePath) == false)
                        {
                            Directory.CreateDirectory(currentSavePath);
                        }
                        string localfile = currentSavePath + "\\" + GetFileName(path);
                        SetStatus("下载" + remoteFile);
                        new HttpHelper().DownLoad(remoteFile, localfile);
                        if(localfile.EndsWith(".css"))
                        {
                            cssFiles.Add(localfile);
                        }
                        if (localfile.EndsWith(".js"))
                        {
                            jsFiles.Add(localfile);
                        }
                    }
                }
            }
        }

        private string UserMd5(string str)
        {
            string cl = str;
            string pwd = "";
            MD5 md5 = MD5.Create();//实例化一个md5对像
            // 加密后是一个字节类型的数组，这里要注意编码UTF8/Unicode等的选择　
            byte[] s = md5.ComputeHash(Encoding.UTF8.GetBytes(cl));
            // 通过使用循环，将字节类型的数组转换为字符串，此字符串是常规字符格式化所得
            for (int i = 0; i < s.Length; i++)
            {
                // 将得到的字符串使用十六进制类型格式。格式后的字符是小写的字母，如果使用大写（X）则格式后的字符是大写字符
                pwd = pwd + s[i].ToString("X");

            }
            return pwd;
        }
        private void SetStatus(string file)
        {
            if(lbl_status.InvokeRequired)
            {
                Action<string> act = SetStatus;
                lbl_status.Invoke(act, file);
            }
            else
            {
                lbl_status.Text = file;
            }
        }

        private string GetFileName(string url)
        {
            if (url.IndexOf("?") > -1)
            {
                url = url.Substring(0, url.IndexOf("?"));
            }
            if(url.EndsWith("/"))
            {
                return "index.html";
            }
            string currentDir = GetCurrentUrl(url);
            if(currentDir == "")
            {
                return url;
            }
            string fileName = url.Substring(currentDir.Length + 1);
            if(fileName.IndexOf(".") < 0)
            {
                fileName = fileName + ".html";
            }
            return fileName;
        }

        private string GetCurrentUrl(string url)
        {
            if(url.IndexOf("?") > -1)
            {
                url = url.Substring(0, url.IndexOf("?"));
            }
            if(url.EndsWith("/"))
            {
                return url.TrimEnd('/');
            }
            string[] ps = url.Split('/');
            string ret = "";
            for (int i = 0; i < ps.Length - 1; i++)
            {
                ret += ps[i] + "/";
            }
            ret = ret.TrimEnd('/');
            return ret;
        }

        private string GetBaseUrl(string url)
        {
            string prefix = "http://";
            if(url.IndexOf("https://") > -1)
            {
                prefix = "https://";
            }
            url = url.Replace(prefix, "");
            url = url.Substring(0, url.IndexOf("/"));
            return prefix + url;
        }
    }
}
