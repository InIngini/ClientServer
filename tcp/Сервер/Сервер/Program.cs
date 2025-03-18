using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class User
{
    private Socket socket;

    public User(Socket socket)
    {
        this.socket = socket;
        Task.Run(() => StartListening());
        Server.NewUser(this);
    }

    private async Task StartListening()
    {
        byte[] buffer = new byte[1024];
        while (true)
        {
            try
            {
                int bytesRead = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                if (bytesRead > 0)
                {
                    string messages = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string[] messageArray = messages.Split(new string[] { "#" }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string message in messageArray)
                    {
                        // Обработка каждого сообщения
                        Server.ProcessClientMessage(message,socket);
                    }
                    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
                break;
            }
        }
        Server.EndUser(this);
    }
}

public static class Server
{
    public static int CountUsers = 0;
    public delegate void UserEvent(string Name);
    public static List<User> UserList = new List<User>();
    public static Socket ServerSocket;
    public static string Host = "0.0.0.0";
    public const int Port = 8001;
    public static bool Work = true;
    public static int n;

    private static SemaphoreSlim semaphore;
    static Server()
    {
        Random random = new Random();
        n = random.Next(10, 20);
        semaphore = new SemaphoreSlim(n); // Здесь 5 - максимальное количество одновременных запросов
    }
    public static void NewUser(User usr)
    {
        if (UserList.Contains(usr))
            return;
        UserList.Add(usr);
    }

    public static void EndUser(User usr)
    {
        if (!UserList.Contains(usr))
            return;
        UserList.Remove(usr);
    }

    private static SemaphoreSlim semaphoreWait = new SemaphoreSlim(1, 1); // Создание семафора с одним разрешением в качестве критической секции
    private static bool messageSent = false; // Переменная для отслеживания отправки сообщения "#more"
    public static async Task ProcessClientMessage(string message, Socket clientSocket)
    {
        await semaphoreWait.WaitAsync();
        if (semaphore.CurrentCount == 0 && !messageSent)
        {
            // Если все семафоры заняты и сообщение еще не отправлено
            byte[] responseData = Encoding.UTF8.GetBytes("#more");
            await clientSocket.SendAsync(new ArraySegment<byte>(responseData), SocketFlags.None);
            messageSent = true; // Устанавливаем флаг, что сообщение было отправлено
            while(semaphore.CurrentCount != n-1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5000));
            }
        }
        semaphoreWait.Release();
        messageSent = false; // Устанавливаем флаг обратно в false

        await semaphore.WaitAsync(); // Ожидаем разрешения от семафора
        try
        {   
            string[] parts = message.Split('|');
            if (parts.Length >= 3 && parts[0] == "message")
            {
                string name = parts[1]; // Название текста
                string text = parts[2]; // Текст

                // Имитация задержки для проверки на палиндром
                Random random = new Random();
                await Task.Delay(TimeSpan.FromMilliseconds(random.Next(1000, 5000))); // Задержка от 100 до 1000 миллисекунд

                // Проверяем, является ли текст палиндромом
                bool isPalindrome = IsPalindrome(text);

                // Отправляем ответ клиенту
                string response = $"#msg|{name}|{(isPalindrome ? "Палиндром" : "Не палиндром")}";
                Console.WriteLine($"Ответ клиенту: {response}");

                // Код для отправки ответа обратно клиенту
                byte[] responseData = Encoding.UTF8.GetBytes(response);
                await clientSocket.SendAsync(new ArraySegment<byte>(responseData), SocketFlags.None);

            }
        }
        finally
        {
            semaphore.Release(); // Освобождаем семафор после завершения обработки
        }
    }


    private static bool IsPalindrome(string text)
    {
        string cleanText = text.Replace(" ", "").ToLower();
        int length = cleanText.Length;
        for (int i = 0; i < length / 2; i++)
        {
            if (cleanText[i] != cleanText[length - i - 1])
            {
                return false;
            }
        }
        return true;
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        // Получаем имя хоста
        string hostName = Dns.GetHostName();

        // Получаем IP-адреса, связанные с данным хостом
        IPAddress[] ipAddresses = Dns.GetHostAddresses(hostName);

        // Находим IPv4-адрес локального подключения
        foreach (IPAddress address1 in ipAddresses)
        {
            if (address1.AddressFamily == AddressFamily.InterNetwork)
            {
                Server.Host=$"{address1}";
                break;
            }
        }
        IPAddress address = IPAddress.Parse(Server.Host);
        Server.ServerSocket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        Server.ServerSocket.Bind(new IPEndPoint(address, Server.Port));
        Server.ServerSocket.Listen(100);
        Console.WriteLine($"Сервер запущен на {Server.Host}:{Server.Port}. Не забудьте указать адрес в коде клиента!");
        Console.WriteLine("Ожидание подключений...");

        while (Server.Work)
        {
            Socket handle = Server.ServerSocket.Accept();
            Console.WriteLine($"Новое подключение: {handle.RemoteEndPoint.ToString()}");
            new User(handle);
        }
        Console.WriteLine("Сервер отключен.");
    }
}

