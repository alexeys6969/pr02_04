using System;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Common;
using Newtonsoft.Json;

namespace Client
{
    class Program
    {
        public static IPAddress IpAddress;
        public static int Port;
        public static int Id = -1;

       static void Main(string[] args)
        {
            Console.Write("Введите IP адрес сервера: ");
            string sIpAdress = Console.ReadLine();
            Console.Write("Введите порт: ");
            string sPort = Console.ReadLine();
            if (int.TryParse(sPort, out Port) && IPAddress.TryParse(sIpAdress, out IpAddress))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Данные успешно введеыны. Подключаюсь к серверу.");
                while (true)
                {
                    ConnectServer();
                }
            }
        }

        public static bool CheckCommand(string message)
        {
            bool BCommand = false;
            string[] DataMessage = message.Split(new string[1] { " " }, StringSplitOptions.None);
            if (DataMessage.Length > 0)
            {
                string Command = DataMessage[0];
                if (Command == "connect")
                {
                    if (DataMessage.Length != 3)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Использование: connect [login] [password]\nПример: connect User1 P@ssw0rd");
                        BCommand = false;
                    }
                    else
                        BCommand = true;
                }
                else if(Command == "cd")
                    BCommand = true;
                else if (Command == "get")
                {
                    if (DataMessage.Length == 1)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Использование: get [NameFile]\nПример: get Test.txt");
                        BCommand = false;
                    }
                    else BCommand = true;
                }
                else if (Command == "set")
                {
                    if(DataMessage.Length == 1)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Использование: set [NameFile]\nПример: set Test.txt");
                        BCommand = false;
                    }
                    else 
                        BCommand = true;
                }
            }
            return BCommand;
        }

        public static void ConnectServer()
        {
            try
            {
                IPEndPoint endPoint = new IPEndPoint(IpAddress, Port);
                Socket socket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp);
                socket.Connect(endPoint);
                if (socket.Connected)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    string message = Console.ReadLine();
                    if (CheckCommand(message))
                    {
                        ViewModelSend viewModelSend = new ViewModelSend(message, Id);
                        if (message.Split(new string[1] { " " }, StringSplitOptions.None)[0] == "set")
                        {
                            string[] DataMessage = message.Split(new string[1] { " " }, StringSplitOptions.None);
                            string NameFile = "";
                            for (int i = 1; i < DataMessage.Length; i++)
                                if (NameFile == "")
                                    NameFile += DataMessage[i];
                                else NameFile += " " + DataMessage[i];

                            if (File.Exists(NameFile))
                            {
                                FileInfo FileInfo = new FileInfo(NameFile);
                                FileInfoFTP NewFileInfo = new FileInfoFTP(File.ReadAllBytes(NameFile), FileInfo.Name);
                                viewModelSend = new ViewModelSend(JsonConvert.SerializeObject(NewFileInfo), Id);
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Указанный файл не существует");
                            }
                        }
                        byte[] messageByte = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend));
                        int BytesSend = socket.Send(messageByte);
                        byte[] bytes = new byte[10485760];
                        int BytesRec = socket.Receive(bytes);
                        string messageServer = Encoding.UTF8.GetString(bytes, 0, BytesRec);
                        ViewModelMessage viewModelMessage = JsonConvert.DeserializeObject<ViewModelMessage>(messageServer);

                        if (viewModelMessage.Command == "autorization")
                            Id = int.Parse(viewModelMessage.Data);
                        else if (viewModelMessage.Command == "message")
                            Console.WriteLine(viewModelMessage.Data);
                        else if (viewModelMessage.Command == "cd")
                        {
                            List<string> FoldersFiles = new List<string>();
                            FoldersFiles = JsonConvert.DeserializeObject<List<string>>(viewModelMessage.Data);
                            foreach (string Name in FoldersFiles)
                                Console.Write(Name);
                        }
                        else if (viewModelMessage.Command == "file")
                        {
                            string[] DataMessage = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                            string getFile = "";
                            for (int i = 1; i < DataMessage.Length; i++)
                                if (getFile == "")
                                    getFile = DataMessage[i];
                                else
                                    getFile += " " + DataMessage[i];
                            byte[] byteFile = JsonConvert.DeserializeObject<byte[]>(viewModelMessage.Data);
                            File.WriteAllBytes(getFile, byteFile);  
                        }
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("ПОдключение не удалось.");
                }
                socket.Close();
            }
            catch (Exception exp)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Что-то случилось: " + exp.Message);
            }
        }
    }
}