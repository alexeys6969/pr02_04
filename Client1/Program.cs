using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Common;
using Newtonsoft.Json;
using System.Collections.Generic;

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
            string sIpAddress = Console.ReadLine();

            Console.Write("Введите порт: ");
            string sPort = Console.ReadLine();

            if (int.TryParse(sPort, out Port) &&
                IPAddress.TryParse(sIpAddress, out IpAddress))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Данные успешно введены. Подключаюсь к серверу.");

                while (true)
                {
                    ConnectServer();
                }
            }
            else
            {
                Console.WriteLine("Неверный IP-адрес или порт.");
            }
        }

        // Получение полного пакета данных
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
                    throw new Exception("Соединение закрыто сервером.");

                received += count;
            }

            return buffer;
        }

        // Отправка сообщения с указанием его размера
        public static void SendPacket(Socket socket, string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);

            byte[] header = BitConverter.GetBytes(
                IPAddress.HostToNetworkOrder(bytes.Length));

            SendExact(socket, header);
            SendExact(socket, bytes);
        }

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
                    throw new Exception("Не удалось отправить данные.");

                sent += count;
            }
        }

        // Получение сообщения
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

        public static bool CheckCommand(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            string[] parts = message.Split(
                new char[] { ' ' },
                2,
                StringSplitOptions.RemoveEmptyEntries);

            string command = parts[0];

            if (command == "connect")
            {
                string[] data = message.Split(
                    new char[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

                if (data.Length != 3)
                {
                    Console.WriteLine(
                        "Использование: connect [login] [password]");
                    return false;
                }

                return true;
            }

            if (command == "cd")
                return true;

            if (command == "get" || command == "set")
            {
                if (parts.Length < 2)
                {
                    Console.WriteLine(
                        $"Использование: {command} [NameFile]");
                    return false;
                }

                return true;
            }

            Console.WriteLine("Неизвестная команда.");
            return false;
        }

        public static void ConnectServer()
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.White;

                string message = Console.ReadLine();

                if (!CheckCommand(message))
                    return;

                string command = message.Split(' ')[0];

                ViewModelSend viewModelSend =
                    new ViewModelSend(message, Id);

                // Загрузка файла на сервер
                if (command == "set")
                {
                    string filePath = message.Substring(4).Trim();

                    if (!File.Exists(filePath))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Указанный файл не существует.");
                        return;
                    }

                    FileInfo fileInfo = new FileInfo(filePath);

                    FileInfoFTP newFileInfo = new FileInfoFTP(
                        File.ReadAllBytes(filePath),
                        fileInfo.Name);

                    viewModelSend = new ViewModelSend(
                        JsonConvert.SerializeObject(newFileInfo),
                        Id);
                }

                using (Socket socket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp))
                {
                    socket.Connect(new IPEndPoint(IpAddress, Port));

                    string json = JsonConvert.SerializeObject(viewModelSend);

                    SendPacket(socket, json);

                    string response = ReceivePacket(socket);

                    ViewModelMessage viewModelMessage =
                        JsonConvert.DeserializeObject<ViewModelMessage>(response);

                    if (viewModelMessage == null)
                        throw new Exception("Некорректный ответ сервера.");

                    switch (viewModelMessage.Command)
                    {
                        case "autorization":

                            Id = int.Parse(viewModelMessage.Data);

                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Авторизация успешна.");

                            break;

                        case "message":

                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine(viewModelMessage.Data);

                            break;

                        case "cd":

                            List<string> foldersFiles =
                                JsonConvert.DeserializeObject<List<string>>(
                                    viewModelMessage.Data);

                            Console.ForegroundColor = ConsoleColor.Cyan;

                            foreach (string name in foldersFiles)
                            {
                                Console.WriteLine(name);
                            }

                            break;

                        case "file":

                            string fileName = message.Substring(4).Trim();

                            byte[] fileData =
                                JsonConvert.DeserializeObject<byte[]>(
                                    viewModelMessage.Data);

                            string savePath = Path.GetFileName(fileName);

                            File.WriteAllBytes(savePath, fileData);

                            Console.ForegroundColor = ConsoleColor.Green;

                            Console.WriteLine(
                                $"Файл успешно скачан: {savePath}");

                            break;
                    }
                }
            }
            catch (Exception exp)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Ошибка: " + exp.Message);
            }
        }
    }
}
