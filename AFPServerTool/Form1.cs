using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Net.Json;
using System.Net;

namespace AFPServerTool
{
    public partial class Form1 : Form
    {
        const int INGEST = 1;
        const int QUERY = 2;

        public Form1()
        {
            InitializeComponent();

            progressBar1.Minimum = 0;
            progressBar1.Step = 1;
            progressBar1.Value = 0;
        }

        // 파일을 listview 안에 끌고 올 때의 이벤트
        private void listView1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        // 파일을 listview 안에 drag & drop 했을 때의 이벤트(파일 추가 & 파일 정보 리스트 뷰에 추가)
        private void listView1_DragDrop(object sender, DragEventArgs e)
        {
            var insertFilePath = (string[])e.Data.GetData(DataFormats.FileDrop);
            System.IO.FileInfo fileInfo;

            for (int i = 0; i < insertFilePath.Length; i++)
            {
                fileInfo = new System.IO.FileInfo(insertFilePath[i]);
                string fileName = insertFilePath[i].Substring(insertFilePath[i].LastIndexOf('\\') + 1);
                string fileType = fileName.Substring(fileName.LastIndexOf('.') + 1);
                ListViewItem lvi = new ListViewItem(fileName);
                lvi.SubItems.Add(fileType);
                lvi.SubItems.Add(fileInfo.Length.ToString());
                lvi.SubItems.Add(fileInfo.LastWriteTime.ToString());
                lvi.SubItems.Add(insertFilePath[i]);
                listView1.Items.Add(lvi);
            }
        }

        private void listView1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button.Equals(MouseButtons.Right))
            {
                ContextMenu m = new ContextMenu();
                MenuItem m1 = new MenuItem();
                MenuItem m2 = new MenuItem();
                MenuItem m3 = new MenuItem();

                m1.Text = "ingest";
                m2.Text = "delete";
                m3.Text = "query";

                m1.Click += (senders, es) =>
                {
                    ingestMovieFile();
                };

                m2.Click += (senders, es) =>
                {
                    deleteMovieFile();
                };

                m3.Click += (senders, es) =>
                {
                    queryMovieFile();
                };

                m.MenuItems.Add(m1);
                m.MenuItems.Add(m2);
                m.MenuItems.Add(m3);

                m.Show(listView1, new Point(e.X, e.Y));
            }
        }

        private void ingestMovieFile()
        {
            int count = countSelectedFile(listView1);

            progressBar1.Value = 0;
            progressBar1.Maximum = count;
            
            for (int i = 0; i < listView1.Items.Count; i++)
            {
                if (listView1.Items[i].Selected == true)
                {
                    // 배치파일 만들기(codegen)
                    makeBatchForCodegen(listView1.Items[i].SubItems[4].Text);

                    // codegen.exe 실행해서 코드젠으로 변환
                    executeCodegen();

                    // load codegen.txt
                    loadnSubmitCodegenData(listView1.Items[i].SubItems[4].Text, INGEST);

                    progressBar1.PerformStep();
                }
            }
        }

        private void deleteMovieFile()
        {
            int count = countSelectedFile(listView1);

            progressBar1.Value = 0;
            progressBar1.Maximum = count;

            for (int i = 0; i < listView1.Items.Count; i++)
            {
                if (listView1.Items[i].Selected == true)
                {
                    submitDelete2Server(listView1.Items[i].SubItems[4].Text);

                    progressBar1.PerformStep();
                }
            }
        }

        private void queryMovieFile()
        {
            for (int i = 0; i < listView1.Items.Count; i++)
            {
                if (listView1.Items[i].Selected == true)
                    submitQuery2Server(listView1.Items[i].SubItems[4].Text);
            }
        }

        private void makeBatchForCodegen(string path)
        {
            string currentPath = System.Environment.CurrentDirectory;
            // 배치파일 만들어서 안에 내용 쓰고, bat 파일로 저장.(파일 출력)
            StreamWriter bat = new StreamWriter(currentPath + "\\" + "codegen.bat", false, Encoding.GetEncoding(949));
            bat.WriteLine("@echo off");
            bat.WriteLine("cd " + currentPath);
            bat.WriteLine("codegen.exe \"" + path + "\" 0 120");
            bat.WriteLine("exit");
            bat.Close();
        }

        private void executeCodegen()
        {
            string codegenPath = System.Environment.CurrentDirectory + "\\codegen.bat";

            Process p = new Process();
            ProcessStartInfo pinfo = new ProcessStartInfo();

            pinfo.FileName = "cmd";
            pinfo.Arguments = "/k " + "\"" + codegenPath + "\" > codegen.txt";

            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo = pinfo;
            
            p.Start();
            p.WaitForExit();
        }

        private void loadnSubmitCodegenData(string path, int mode)
        {
            string fileName = path.Substring(path.LastIndexOf('\\') + 1);
            string key = fileName.Substring(0, fileName.LastIndexOf('.'));

            string data = System.IO.File.ReadAllText(Path.Combine(Application.StartupPath, "codegen.txt"));
            JsonTextParser parser = new JsonTextParser();
            JsonObject obj = parser.Parse(data);
            JsonArrayCollection col = (JsonArrayCollection)obj;

            string fp, codever;
            foreach (JsonObjectCollection joc in col)
            {
                fp = (string)joc["fp"].GetValue();
                codever = joc["codever"].GetValue().ToString();
                
                if(mode == INGEST)
                    submitIngest2Server(key, fp, codever);
                else
                    submitQuery2Server(fp, codever);
            }
        }

        private void submitIngest2Server(string key, string fp, string codever)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://211.110.33.122/admin/ingest");
            httpWebRequest.ContentType = "text/json";
            httpWebRequest.Method = "POST";
            
            string program_name = key.Substring(0, key.LastIndexOf('_'));
            string s_program_entry = key.Substring(key.LastIndexOf('_') + 1, 4);
            int program_entry = Convert.ToInt32(s_program_entry);

            
            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                // program_name program_entry 도 같이
                string json = "{\"password\":\"1q2w3e4r\"," + "\"key\":\"" + key + "\"," + "\"fp\":\"" + fp + "\"," + "\"codever\":\"" + codever + "\","
                    + "\"program_name\":\"" + program_name + "\"," + "\"program_entry\":\"" + program_entry.ToString() + "\"}";
                
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                }
            }
        }

        private void submitQuery2Server(string fp, string codever)
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://211.110.33.122/query");
            httpWebRequest.ContentType = "text/json";
            httpWebRequest.Method = "POST";

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string json = "{\"fp\":\"" + fp + "\"," + "\"codever\":\"" + codever + "\"}";
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    MessageBox.Show(result.ToString());
                }
            }
        }

        private void submitDelete2Server(string path)
        {
            string fileName = path.Substring(path.LastIndexOf('\\') + 1);
            string key = fileName.Substring(0, fileName.LastIndexOf('.'));

            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://211.110.33.122/admin/delete");
            httpWebRequest.ContentType = "text/json";
            httpWebRequest.Method = "POST";

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string json = "{\"password\":\"1q2w3e4r\"," + "\"key\":\"" + key + "\"}";

                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                }
            }
        }

        private void submitQuery2Server(string path)
        {
            makeBatchForCodegen(path);

            executeCodegen();

            loadnSubmitCodegenData(path, QUERY);
        }

        private int countSelectedFile(ListView listView1)
        {
            int count = 0;

            for (int i = 0; i < listView1.Items.Count; i++)
            {
                if (listView1.Items[i].Selected == true)
                    count = count + 1;
            }

            return count;
        }
    }
}