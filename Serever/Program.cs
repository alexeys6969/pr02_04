using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using Common;
using Newtonsoft.Json;
using Serever;

namespace Server
{
    class Programm
    {
        public static List<User> Users = new List<User>();
        public static IPAddress IpAddress;
        public static int Port;

        static void Main(string[] args)
        {
            Users.Add(new User("leshka", "1111", @"A:\Авиатехникум"));
            Console.Write("Введите IP фдрес сервера: ");
            string sIpAddress = Console.ReadLine();
            Console.Write("Введите порт:");
            string sPort = Console.ReadLine();
            if(int.TryParse(sPort, out Port) && IPAddress.TryParse(sIpAddress, out IpAddress)) 
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Данные успешно введенны. Запускаю сервер: ");
                StartSever();
            }
            Console.Read();
        }

        public static bool AutorizationUser(string login, string password)
        {
            User user = null;
            user = Users.Find(x => x.login == login && x.password == password);
            return user != null;
        }

        public static List<string> GetDirectory(string src)
        {
            List<string> FoldersFiles = new List<string>();
            if(Directory.Exists(src))
            {
                string[] dirs = Directory.GetDirectories(src);
                foreach(string dir in dirs)
                {
                    string NameDirectory = dir.Replace(src, "");
                    FoldersFiles.Add(NameDirectory + "/");
                }
                string[] files = Directory.GetFiles(src);
                foreach (string file in files)
                {
                    string NameFile = file.Replace(src, "");
                    FoldersFiles.Add(NameFile);
                }
            }
            return FoldersFiles;
        }

        public static void StartSever()
        {
            IPEndPoint endPoint = new IPEndPoint(IpAddress, Port);
            Socket sListener = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);
            sListener.Bind(endPoint);
            sListener.Listen(10);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Сервер запущен");
            while (true)
            {
                try
                {
                    Socket Handler = sListener.Accept();
                    string Data = null;
                    byte[] Bytes = new byte[10485760];
                    int BytesRec = Handler.Receive(Bytes);
                    Data += Encoding.UTF8.GetString(Bytes, 0, BytesRec);
                    Console.Write("Сообщение от пользователя: " + Data + "\n");
                    string Reply = "";
                    ViewModelSend viewModelSend = JsonConvert.DeserializeObject<ViewModelSend>(Data);
                    if(viewModelSend != null)
                    {
                        ViewModelMessage viewModelMessage;
                        string[] DataCommand = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                        if (DataCommand[0] == "connect")
                        {
                            string[] DataMessage = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);

                            if (AutorizationUser(DataMessage[1], DataMessage[2]))
                            {
                                int IdUser = Users.FindIndex(x => x.login == DataMessage[1] && x.password == DataMessage[2]);
                                viewModelMessage = new ViewModelMessage("autorization", IdUser.ToString());
                            } 
                            else
                            {
                                viewModelMessage = new ViewModelMessage("message", "Неправильный логин или пароль");
                            }
                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        } 
                        else if(DataCommand[0] == "cd")
                        {
                            if(viewModelSend.Id != -1)
                            {
                                string[] DataMessage = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                                List<string> FoldersFiles = new List<string>();
                                if(DataMessage.Length == 1)
                                {
                                    Users[viewModelSend.Id].temp_src = Users[viewModelSend.Id].src;
                                    FoldersFiles = GetDirectory(Users[viewModelSend.Id].src);
                                } 
                                else
                                {
                                    string cdFolder = "";
                                    for(int i = 1; i < DataMessage.Length; i++)
                                    {
                                        if (cdFolder == "")
                                            cdFolder += DataMessage[i];
                                        else
                                            cdFolder += " " + DataMessage[i];
                                    }
                                    Users[viewModelSend.Id].temp_src = Users[viewModelSend.Id].temp_src + cdFolder;
                                    FoldersFiles = GetDirectory(Users[viewModelSend.Id].temp_src);
                                }
                                if (FoldersFiles.Count == 0)
                                    viewModelMessage = new ViewModelMessage("message", "Директория пуста или не сущетсвует");
                                else
                                    viewModelMessage = new ViewModelMessage("cd", JsonConvert.SerializeObject(FoldersFiles));
                            }
                            else
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);

                        } else if (DataCommand[0] == "get")
                        {
                            if (viewModelSend.Id != -1)
                            {
                                string[] DataMessage = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                                string getFile = "";
                                for (int i = 1; i < DataMessage.Length; i++)
                                    if (getFile == "")
                                        getFile += DataMessage[i];
                                    else
                                        getFile += " " + DataMessage[i];

                                byte[] byteFile = File.ReadAllBytes(Users[viewModelSend.Id].temp_src + getFile);
                                viewModelMessage = new ViewModelMessage("file", JsonConvert.SerializeObject(byteFile));
                            }
                            else
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");

                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                        else
                        {
                            if(viewModelSend.Id != -1)
                            {
                                FileInfoFTP SendFileInfo = JsonConvert.DeserializeObject<FileInfoFTP>(viewModelSend.Message);
                                File.WriteAllBytes(Users[viewModelSend.Id].temp_src + @"\" + SendFileInfo.Name, SendFileInfo.Data);
                                viewModelMessage = new ViewModelMessage("message", "Файл загружен");
                            }
                            else
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");

                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                    }
                } catch(Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Что-то случилось: " + e.Message);
                }
            }
        }
    }
}

