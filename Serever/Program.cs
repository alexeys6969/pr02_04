
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
            Users.Add(new User(
                "leshka",
                "1111",
                @"C:\Users\Алексей\Desktop\123\"));

            Console.Write("Введите IP адрес сервера: ");
            string sIpAddress = Console.ReadLine();

            Console.Write("Введите порт: ");
            string sPort = Console.ReadLine();

            if (int.TryParse(sPort, out Port) &&
                IPAddress.TryParse(sIpAddress, out IpAddress))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Данные успешно введены.");

                StartSever();
            }
            else
            {
                Console.WriteLine("Неверный IP-адрес или порт.");
            }
        }

        // Авторизация
        public static bool AutorizationUser(string login, string password)
        {
            return Users.Any(x =>
                x.login == login &&
                x.password == password);
        }

        // Получение файлов и папок
        public static List<string> GetDirectory(string src)
        {
            List<string> result = new List<string>();

            if (!Directory.Exists(src))
                return result;

            foreach (string dir in Directory.GetDirectories(src))
            {
                result.Add(Path.GetFileName(dir) + "/");
            }

            foreach (string file in Directory.GetFiles(src))
            {
                result.Add(Path.GetFileName(file));
            }

            return result;
        }

        // Получение полного пути с проверкой доступа к корневой папке
        public static string GetSafePath(User user, string name)
        {
            string root = Path.GetFullPath(user.src);

            string current = Path.GetFullPath(user.temp_src);

            string target = Path.GetFullPath(
                Path.Combine(current, name));

            string rootPrefix = Path.TrimEndingDirectorySeparator(root)
                + Path.DirectorySeparatorChar;

            if (!target.Equals(root, StringComparison.OrdinalIgnoreCase) &&
                !target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Выход за пределы рабочей директории запрещён.");
            }

            return target;
        }

        // Получение полного пакета
        public static byte[] ReceiveExact(Socket socket, int length)
        {
            byte[] buffer = new byte[length];
            int received = 0;

            while (received < length)
            {
                int count = socket.Receive(
                    buffer,
                    received,
                    length - received,
                    SocketFlags.None);

                if (count == 0)
                    throw new Exception("Клиент отключился.");

                received += count;
            }

            return buffer;
        }

        // Отправка данных
        public static void SendExact(Socket socket, byte[] data)
        {
            int sent = 0;

            while (sent < data.Length)
            {
                int count = socket.Send(
                    data,
                    sent,
                    data.Length - sent,
                    SocketFlags.None);

                if (count == 0)
                    throw new Exception("Ошибка отправки данных.");

                sent += count;
            }
        }

        // Отправка пакета с размером
        public static void SendPacket(Socket socket, string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);

            byte[] header = BitConverter.GetBytes(
                IPAddress.HostToNetworkOrder(bytes.Length));

            SendExact(socket, header);
            SendExact(socket, bytes);
        }

        // Получение пакета
        public static string ReceivePacket(Socket socket)
        {
            byte[] header = ReceiveExact(socket, 4);

            int length = IPAddress.NetworkToHostOrder(
                BitConverter.ToInt32(header, 0));

            if (length < 0 || length > 50 * 1024 * 1024)
                throw new Exception("Некорректный размер сообщения.");

            byte[] data = ReceiveExact(socket, length);

            return Encoding.UTF8.GetString(data);
        }

        public static void StartSever()
        {
            IPEndPoint endPoint = new IPEndPoint(IpAddress, Port);

            Socket listener = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);

            listener.Bind(endPoint);
            listener.Listen(10);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Сервер запущен.");

            while (true)
            {
                try
                {
                    using (Socket handler = listener.Accept())
                    {
                        string data = ReceivePacket(handler);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Сообщение от пользователя: " + data);

                        ViewModelSend viewModelSend =
                            JsonConvert.DeserializeObject<ViewModelSend>(data);

                        if (viewModelSend == null)
                            continue;

                        ViewModelMessage response = null;

                        string message = viewModelSend.Message;

                        string[] parts = message.Split(
                            new char[] { ' ' },
                            2,
                            StringSplitOptions.RemoveEmptyEntries);

                        string command = parts.Length > 0 ? parts[0] : "";

                        // CONNECT
                        if (command == "connect")
                        {
                            string[] loginData = message.Split(
                                new char[] { ' ' },
                                StringSplitOptions.RemoveEmptyEntries);

                            if (loginData.Length == 3 &&
                                AutorizationUser(loginData[1], loginData[2]))
                            {
                                int userId = Users.FindIndex(x =>
                                    x.login == loginData[1] &&
                                    x.password == loginData[2]);

                                Users[userId].temp_src = Users[userId].src;

                                response = new ViewModelMessage(
                                    "autorization",
                                    userId.ToString());
                            }
                            else
                            {
                                response = new ViewModelMessage(
                                    "message",
                                    "Неправильный логин или пароль.");
                            }
                        }

                        // CD
                        else if (command == "cd")
                        {
                            if (viewModelSend.Id < 0 ||
                                viewModelSend.Id >= Users.Count)
                            {
                                response = new ViewModelMessage(
                                    "message",
                                    "Необходимо авторизоваться.");
                            }
                            else
                            {
                                User user = Users[viewModelSend.Id];

                                string target;

                                if (parts.Length == 1)
                                {
                                    target = Path.GetFullPath(user.src);
                                }
                                else
                                {
                                    target = GetSafePath(user, parts[1]);
                                }

                                if (!Directory.Exists(target))
                                {
                                    response = new ViewModelMessage(
                                        "message",
                                        "Директория не существует.");
                                }
                                else
                                {
                                    user.temp_src = target;

                                    List<string> files = GetDirectory(target);

                                    response = new ViewModelMessage(
                                        "cd",
                                        JsonConvert.SerializeObject(files));
                                }
                            }
                        }

                        // GET
                        else if (command == "get")
                        {
                            if (viewModelSend.Id < 0 ||
                                viewModelSend.Id >= Users.Count)
                            {
                                response = new ViewModelMessage(
                                    "message",
                                    "Необходимо авторизоваться.");
                            }
                            else if (parts.Length < 2)
                            {
                                response = new ViewModelMessage(
                                    "message",
                                    "Укажите имя файла.");
                            }
                            else
                            {
                                User user = Users[viewModelSend.Id];

                                string filePath = GetSafePath(user, parts[1]);

                                if (!File.Exists(filePath))
                                {
                                    response = new ViewModelMessage(
                                        "message",
                                        "Файл не найден.");
                                }
                                else
                                {
                                    byte[] fileData = File.ReadAllBytes(filePath);

                                    response = new ViewModelMessage(
                                        "file",
                                        JsonConvert.SerializeObject(fileData));
                                }
                            }
                        }

                        // SET
                        else
                        {
                            if (viewModelSend.Id < 0 ||
                                viewModelSend.Id >= Users.Count)
                            {
                                response = new ViewModelMessage(
                                    "message",
                                    "Необходимо авторизоваться.");
                            }
                            else
                            {
                                User user = Users[viewModelSend.Id];

                                FileInfoFTP fileInfo =
                                    JsonConvert.DeserializeObject<FileInfoFTP>(
                                        message);

                                if (fileInfo == null ||
                                    string.IsNullOrWhiteSpace(fileInfo.Name) ||
                                    fileInfo.Data == null)
                                {
                                    response = new ViewModelMessage(
                                        "message",
                                        "Некорректный файл.");
                                }
                                else
                                {
                                    string fileName = Path.GetFileName(fileInfo.Name);

                                    if (fileName != fileInfo.Name)
                                        throw new Exception("Некорректное имя файла.");

                                    string filePath = GetSafePath(user, fileName);

                                    File.WriteAllBytes(filePath, fileInfo.Data);

                                    response = new ViewModelMessage(
                                        "message",
                                        "Файл успешно загружен на сервер.");
                                }
                            }
                        }

                        if (response != null)
                        {
                            string reply = JsonConvert.SerializeObject(response);

                            SendPacket(handler, reply);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Ошибка: " + e.Message);
                }
            }
        }
    }
}
