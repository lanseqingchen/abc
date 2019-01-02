using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.IO;

namespace LightCtrl
{
    public partial class socket : Form
    {
        private static String[] str={"10","00","000","00","10","10","00","000","00","10"};
        private int flag_Data_Control = 2;
        private IPAddress serverip = IPAddress.Parse("127.0.0.1");//定义并初始化IP地址
        private IPEndPoint serverFullAddr;//存储IP和端口用，为一个集合
        private Boolean flag = false;//变量的两种状态，true和false
        private Socket sock;//定义全局变量socket对象
        public Dictionary<string, Socket> clients = new Dictionary<string, Socket>();   // 存储连接到服务器的客户端信息，为一集合
        Thread myThread = null;//定义新线程，初始化为null
        Thread myThread0 = null;
        Int16 flag0 = 0;
        public socket()
        {
            InitializeComponent();
        }
       
        private void socket_Load(object sender, EventArgs e)
        {
           
        }

        private void button1_Click(object sender, System.EventArgs e)
        {
            /*socket套接字的配置****************************************/
            serverip = IPAddress.Parse(textBox1.Text);//获取文本框输入的IP
            serverFullAddr = new IPEndPoint(serverip, int.Parse(textBox2.Text));//将IP与端口写入一个集合
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);//声明定义socket的连接方式
            sock.Bind(serverFullAddr);//配置socket连接
            listBox1.Invoke(new SetTextCallBack(SetText), "启动成功。\n");//调用主线程的委托方法，此语句一般写在子线程中
            flag = true;
            sock.Listen(5);//设置监听队列长度为5
            /******************************************/
            myThread = new Thread(new ThreadStart(BeginListen));//启动一个新线程，线程调用的方法为BeginListen()
            myThread.IsBackground = true;//将线程设置为与窗口主线程同步，即窗口消失，子线程也随之消失
            myThread.Start();//启动线程用于监听客户端连接

            button1.Enabled = false;//失能button1
            button2.Enabled = true;//使能button2
            button3.Enabled = true;//使能button3
            textBox3.ReadOnly = false;//关闭文本框只读
            textBox1.ReadOnly = true;//打开文本框只读
            textBox2.ReadOnly = true;//打开文本框只读
        }
        //监听方法
        private void BeginListen()
        {

            string mess = "";//定义string对象，并初始化，用于作发送或接受使用
            Socket newsocket = null;//定义socket对象，初始化为null
            while (true)
            {
                try//try和catch语句用来尝试一个新的动作，并检测异常
                {
                    //listBox1.Invoke(new SetTextCallBack(SetText), "qwe");
                    //若此代码块中的语句出现异常，若此异常属于SocketException，则会被catch捕捉到，并存在变量le中
                    newsocket = sock.Accept();//有新连接时，创建新的socket连接并赋值给newsocket
                    //newsocket.Close();
                }
                catch (SocketException le)
                {
                    listBox1.Invoke(new SetTextCallBack(SetText), mess + le);//该语句在子线程中运行，功能为返回主线程执行SetText(string text)，text参数为“mess + le”
                    break;
                }
                string clientIp = newsocket.RemoteEndPoint.ToString();
                mess = "已成功和：" + clientIp + "建立连接！";
                listBox1.Invoke(new SetTextCallBack(SetText), mess);
                //IPAddress aa=(newsocket.RemoteEndPoint as IPEndPoint).Address;//获取IP
                //int clientPort = (newsocket.RemoteEndPoint as IPEndPoint).Port;//获取端口
                if (!clients.ContainsKey(clientIp))
                {
                    comboBox1.Invoke(new SetTextCallBack(ComBoxSetText), "" + clientIp);
                    clients.Add(clientIp, newsocket);
                }// 将连接的客户端socket添加到clients中保存  
                else clients[clientIp] = newsocket;
                mess = "连接服务器成功！本地IP为： " + clientIp + " 当前时间为：" + DateTime.Now;
                newsocket.Send(Encoding.Default.GetBytes(mess));
                ParameterizedThreadStart abc = new ParameterizedThreadStart(infoConnection);
                Thread abd = new Thread(abc);

                abd.IsBackground = true;//将线程设置为与窗口主线程同步，即窗口消失，子线程也随之消失
                abd.Start(newsocket);
                Thread.Sleep(1000);
                if (!flag) { return; }
            }
        }
        //通信方法:接收
        private void infoConnection(object socketclientpara)
        {
            Socket socketserver = socketclientpara as Socket;

            string mess = "";
            while (true)
            {
                byte[] message = new byte[1024];
                int bytes;
                try
                {
                    mess = "";
                    //socketserver = sock.Accept();
                    // listBox1.Invoke(new SetTextCallBack(SetText), "liu");
                    bytes = socketserver.Receive(message);
                    string clientIp = socketserver.RemoteEndPoint.ToString();
                    if (bytes <= 0)
                    {
                        mess = "客户端断开了连接， " + mess + " 来自：" + clientIp + " 当前时间为：" + DateTime.Now;
                        listBox1.Invoke(new SetTextCallBack(SetText), mess);
                        clients.Remove(socketserver.RemoteEndPoint.ToString());
                        comboBox1.Invoke(new SetTextCallBack(ComBoxRemoveText), socketserver.RemoteEndPoint.ToString());
                        socketserver.Close();
                        comboBox1.Invoke(new SetTextCallBack(ComBoxSetText00), "1");

                        return;
                    }
                    mess = Encoding.Default.GetString(message, 0, bytes);
                    WriteSaveText(mess);
                    mess = "已接收数据： " + mess + " 来自：" + clientIp + " 当前时间为：" + DateTime.Now;
                    listBox1.Invoke(new SetTextCallBack(SetText), mess);
                    //socketserver.Send(Encoding.Default.GetBytes(mess));

                    //newsocket.Close();
                }
                catch (SocketException le)
                {

                    listBox1.Invoke(new SetTextCallBack(SetText), "原因：" + mess + le);
                    clients.Remove(socketserver.RemoteEndPoint.ToString());
                    comboBox1.Invoke(new SetTextCallBack(ComBoxRemoveText), socketserver.RemoteEndPoint.ToString());
                    socketserver.Close();
                    break;
                }
                Thread.Sleep(200);
                if (!flag)
                {
                    clients.Remove(socketserver.RemoteEndPoint.ToString());
                    comboBox1.Invoke(new SetTextCallBack(ComBoxRemoveText), socketserver.RemoteEndPoint.ToString());
                    socketserver.Close();
                    return;
                }

            }
        }

        private void button3_Click(object sender, System.EventArgs e)
        {
            //if (comboBox1.SelectedItem == null)
            //{
            //    listBox1.Invoke(new SetTextCallBack(SetText), "发送失败，客户端地址不能为空！");
            //    return;
            //}
            sen_check(textBox3.Text);
        }
        private void sen_check(string message)
        {
            int i=0,n;
            //n=comboBox1.Items.Count;
            //while (i < n)
            //{
                
                try
                {


                    if (clients.ContainsKey(comboBox1.SelectedItem.ToString()))
                        send_client(clients[comboBox1.SelectedItem.ToString()], textBox3.Text);
                    else
                    {
                        listBox1.Invoke(new SetTextCallBack(SetText), "发送失败，请检查客户端是否在线！");
                        return;
                    }
                    //if (clients.ContainsKey(comboBox1.Items[i].ToString()))
                    //    send_client(clients[comboBox1.Items[i].ToString()], message);
                    //else
                    //{
                    //    listBox1.Invoke(new SetTextCallBack(SetText), "发送失败，请检查客户端是否在线！");
                    //    return;
                    //}
                }
                catch (Exception le)
                {
                    listBox1.Invoke(new SetTextCallBack(SetText), "发送异常：" + le);
                    clients.Remove(comboBox1.Items[i].ToString());
                    comboBox1.Invoke(new SetTextCallBack(ComBoxRemoveText), comboBox1.Items[i].ToString());
                }
            //    i++;
            //}
        }
        private void send_client(Socket socket, string data)
        {
            if (socket != null && data != null && !data.Equals(""))
            {
                byte[] bytes = Encoding.Default.GetBytes(data);   // 将data转化为byte数组  
                socket.Send(bytes);
            }
            else
            {
                listBox1.Invoke(new SetTextCallBack(SetText), "发送内容不能为空！");
            }
        }
        #region 用于在文本框上显示数据
        delegate void SetTextCallBack(string text);
        private void SetText(string text)
        {
            listBox1.Items.Add(text);
        }
        private void SetText1(string text)
        {
            listBox2.Items.Add(text);
        }
        private void ComBoxSetText(string text)
        {
            comboBox1.Items.Add(text);
        }
        private void ComBoxSetText00(string text)
        {
            comboBox1.Text = "-----请选择客户端-----";
        }
        private void ComBoxRemoveText(string text)
        {
            comboBox1.Items.Remove(text);
        }
       
        #endregion
        private void WriteSaveText(string text)
        {
            int n = text.Length;
            if(n>=24)
            if (text.Substring(2, 1).Equals("0"))
            {
               // str[0] = "0"+text.Substring(2, 1);
                str[1] = "0"+text.Substring(6, 1);
                str[2] = text.Substring(10, 3);
                str[3] = text.Substring(16, 2);
                //str[4] = "0"+
                if (flag_Data_Control == 2)
                    if (text.Substring(23, 1).Equals("1"))
                    {
                        str[0] = "10"; str[4] = "10";
                    }
                    else
                    {
                        str[0] = "00"; str[4] = "00";
                    }
                else
                    flag_Data_Control++;

            }
            else
            {
              //  str[5] = "0" + text.Substring(2, 1);
                str[6] = "0" + text.Substring(6, 1);
                str[7] = text.Substring(10, 3);
                str[8] = text.Substring(16, 2);
                //str[9] = "0"+text.Substring(23, 1);
                if (flag_Data_Control == 2)
                if (text.Substring(23, 1).Equals("1"))
                {
                    str[5] = "10"; str[9] = "10";
                }
                else
                {
                    str[5] = "00"; str[9] = "00";
                }
                else
                     flag_Data_Control ++;
            }
            //comboBox1.Items.Remove(text);
        }
        private void button2_Click(object sender, System.EventArgs e)
        {
            try
            {
                flag = false;
                sock.Close();
                myThread.Abort();
                // socket_Close();
                button1.Enabled = true;
                button2.Enabled = false;
                button3.Enabled = false;
                textBox3.ReadOnly = true;
                textBox1.ReadOnly = false;
                textBox2.ReadOnly = false;
                listBox1.Items.Add("停止成功" + DateTime.Now);
                comboBox1.Text = "-----请选择客户端-----";
            }
            catch (Exception ee)
            {
                listBox1.Text = "停止失败" + ee;
            }
        }

        private void button4_Click(object sender, System.EventArgs e)
        {
            flag0 = 1;
            Thread.Sleep(1000);
            myThread0.Abort();
            listBox2.Invoke(new SetTextCallBack(SetText1), "http服务关闭成功！");
            button5.Enabled = true;
            button4.Enabled = false;
        }

        private void button5_Click(object sender, System.EventArgs e)
        {
            if (myThread0 != null)
                myThread0.Abort();
            myThread0 = new Thread(new ThreadStart(beginListen));
            myThread0.IsBackground = true;
            myThread0.Start();
            
            flag0 = 0;
            button4.Enabled = true;
            button5.Enabled = false;
        }
        private void beginListen()
        {
            try
            {
                HttpListener listerner = new HttpListener();
                listerner.AuthenticationSchemes = AuthenticationSchemes.Anonymous;//指定身份验证  Anonymous匿名访问
                listerner.Prefixes.Add("http://192.168.43.165:8080/web/");
                //listerner.Prefixes.Add("http://192.168.1.100:8080/sliver/");
                // listerner.Prefixes.Add("http://localhost/web/");
                listerner.Start();
                //Console.WriteLine("WebServer Start Successed.......");
                listBox2.Invoke(new SetTextCallBack(SetText1), "http监听启动成功！");
                while (true)
                {
                    //等待请求连接
                    //没有请求则GetContext处于阻塞状态
                    HttpListenerContext ctx = listerner.GetContext();
                    ctx.Response.StatusCode = 200;//设置返回给客服端http状态代码

                    string name = ctx.Request.QueryString["name"];
                    string flag = ctx.Request.QueryString["flag"];
                    string summess = "";
                    string temp="+#+";
                    int i = 0;
                   
                    if (flag != null)
                    {
                        flag_Data_Control = 0;
                        if (flag.Equals("1")) { sen_check("#1O"); str[0] = "10"; str[4] = "10"; temp = "+0+"; }
                        if (flag.Equals("2")) { sen_check("#1C"); str[0] = "00"; str[4] = "00"; temp = "+0+"; }
                        if (flag.Equals("4")) { sen_check("#2O"); str[5] = "10"; str[9] = "10"; temp = "+0+"; }
                        if (flag.Equals("8")) { sen_check("#2C"); str[5] = "00"; str[9] = "00"; temp = "+0+"; }

                        if (flag.Equals("5")) { sen_check("#1O"); str[0] = "10"; str[4] = "10"; sen_check("#2O"); str[5] = "10"; str[9] = "10"; temp = "+0+"; }
                        if (flag.Equals("9")) { sen_check("#1C"); str[0] = "00"; str[4] = "00"; sen_check("#2C"); str[5] = "00"; str[9] = "00"; temp = "+0+"; }
                        if (flag.Equals("6")) { sen_check("#1C"); str[0] = "00"; str[4] = "00"; sen_check("#2O"); str[5] = "10"; str[9] = "10"; temp = "+0+"; }
                        if (flag.Equals("10")) { sen_check("#1C"); str[0] = "00"; str[4] = "00"; sen_check("#2C"); str[5] = "00"; str[9] = "00"; temp = "+0+"; }
                        
                    }
                    for (i = 0; i < 10; i++)
                        summess += str[i];
                    summess +=temp;
                    //if (name != null)
                    //{
                    //    Console.WriteLine(name);

                    //}


                    //使用Writer输出http响应代码
                    try
                    {
                        StreamWriter writer = new StreamWriter(ctx.Response.OutputStream);
                        // Console.WriteLine("aaaaa");
                         listBox2.Invoke(new SetTextCallBack(SetText1),"收到客户端一次请求，已处理，时间："+DateTime.Now.ToString());
                        //writer.WriteLine("<html><head><title>The WebServer</title></head><body>");
                        //writer.WriteLine("<div style=\"height:20px;color:blue;text-align:center;\"><p> chaoxiaoxin {0}</p></div>", name);
                        //writer.WriteLine("<ul>");
                        writer.WriteLine("{0}", summess);
                        //foreach (string header in ctx.Request.Headers.Keys)
                        //{
                        //    writer.WriteLine("<li><b>{0}:</b>{1}</li>", header, ctx.Request.Headers[header]);

                        //}
                        //writer.WriteLine("</ul>");
                        //writer.WriteLine("</body></html>");

                        writer.Close();
                        ctx.Response.Close();
                    }
                    catch (Exception le)
                    {
                        listBox2.Invoke(new SetTextCallBack(SetText1), "原因：" + le);
                    }
                    if (flag0 == 1)
                    {
                        listerner.Stop();
                        listerner.Abort();
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                listBox2.Invoke(new SetTextCallBack(SetText1), "错误，原因：" + e);
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            label5.Text = DateTime.Now.ToString();
        }

        //private void button6_Click(object sender, EventArgs e)
        //{
        //    label5.Text =""+comboBox1.Items[0].ToString() + comboBox1.Items.Count;
        //}
    }
}
