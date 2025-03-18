using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class User
{
    private UdpClient udpClient;
    public IPEndPoint endPoint;

    public User(UdpClient client, IPEndPoint endPoint)
    {
        this.udpClient = client;
        this.endPoint = endPoint;
        StartListening();
    }

    public async Task StartListening()
    {
        while (true)
        {
            try
            {

                UdpReceiveResult receiveResult = await udpClient.ReceiveAsync();
                byte[] receivedData = receiveResult.Buffer;
                string messages = Encoding.UTF8.GetString(receivedData);
                string[] messageArray = messages.Split(new string[] { "#" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string message in messageArray)
                {
                    Server.ProcessClientMessage(message, endPoint, udpClient.Client);
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
    public static List<User> connectedUsers = new List<User>();
    private static UdpClient udpServer;
    public static string Host = "0.0.0.0";
    public const int Port = 8001;
    public static int n;

    private static Random random = new Random(); // Используем один экземпляр Random

    static Server()
    {
        n = random.Next(10, 20); 
        semaphore = new SemaphoreSlim(n); // Инициализация семафора с количеством разрешений n
    }
   
    private static SemaphoreSlim semaphoreWait = new SemaphoreSlim(1, 1); // Создание семафора с одним разрешением в качестве критической секции
    private static bool messageSent = false; // Переменная для отслеживания отправки сообщения "#more"
    private static SemaphoreSlim semaphore;
    public static async Task ProcessClientMessage(string message, EndPoint clientEndPoint, Socket serverSocket)
    {
        await semaphoreWait.WaitAsync();
        if (semaphore.CurrentCount == 0 && !messageSent)
        {
            // Если все семафоры заняты и сообщение еще не отправлено
            byte[] responseData = Encoding.UTF8.GetBytes("#more");
            await serverSocket.SendToAsync(new ArraySegment<byte>(responseData), SocketFlags.None, clientEndPoint);
            messageSent = true; // Устанавливаем флаг, что сообщение было отправлено
            while (semaphore.CurrentCount != n - 1)
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
                await Task.Delay(random.Next(1000, 5000)); // Задержка от 1000 до 5000 миллисекунд

                // Проверяем, является ли текст палиндромом
                bool isPalindrome = IsPalindrome(text);

                // Отправляем ответ клиенту
                string response = $"#msg|{name}|{(isPalindrome ? "Палиндром" : "Не палиндром")}";
                Console.WriteLine($"Ответ клиенту: {response}");

                // Код для отправки ответа обратно клиенту
                byte[] responseData = Encoding.UTF8.GetBytes(response);
                await serverSocket.SendToAsync(new ArraySegment<byte>(responseData), SocketFlags.None, clientEndPoint);
            }
        }
        finally
        {
            semaphore.Release(); // Освобождаем семафор после завершения обработки
        }
    }
    public static void EndUser(User user)
    {
        connectedUsers.Remove(user);
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
                Server.Host = $"{address1}";
                break;
            }
        }
        IPAddress address = IPAddress.Parse(Server.Host);
        UdpClient udpServer = new UdpClient(new IPEndPoint(address, Server.Port));
        Console.WriteLine("UDP сервер запущен.");
        Console.WriteLine($"Сервер запущен на {Server.Host}:{Server.Port}. Не забудьте указать адрес в коде клиента!");
        Console.WriteLine("Ожидание подключения...");
        try
        {
            while (true)
            {
                UdpReceiveResult result = await udpServer.ReceiveAsync();
                IPEndPoint clientEndPoint = result.RemoteEndPoint;
                byte[] receivedData = result.Buffer;
                //Console.WriteLine($"Новый пользователь: {clientEndPoint}");
                // Создаем объект User для обработки соединения с клиентом
                //User user = new User(udpServer, clientEndPoint);
                //Server.connectedUsers.Add(user);
                
                // Проверяем, существует ли пользователь с таким clientEndPoint
                User existingUser = Server.connectedUsers.FirstOrDefault(u => u.endPoint.Equals(clientEndPoint));

                if (existingUser != null)
                {
                    // Пользователь уже существует, вызываем его методы
                    existingUser.StartListening();
                }
                else
                {
                    // Пользователь не существует, создаем нового и добавляем в список подключенных
                    User newUser = new User(udpServer, clientEndPoint);
                    Server.connectedUsers.Add(newUser);

                }
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Ошибка сокета: {ex.Message}");
        }
        finally
        {
            udpServer.Close();
        }

        Console.WriteLine("Сервер отключен.");
    }
}

