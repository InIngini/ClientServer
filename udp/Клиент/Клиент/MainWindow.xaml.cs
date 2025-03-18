using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Клиент
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private delegate void ChatEvent(string content, string clr);
        private Socket _serverSocket; // сокеты
        private Thread listenThread; // поток 
        private string _host = "192.168.1.33"; // IP
        private int _port = 8001; // порт
        private ChatEvent _addMessage; // добавление сообщений 
        public MainWindow()
        {
            InitializeComponent();

            _addMessage = new ChatEvent(AddMessage); //для добавление сообщения 

            resultTextBox.Text = "Истории сообщений еще нет...";

            pathTextBox.Text = "Задайте путь...";
            pathTextBox.GotFocus += TextBox_GotFocus;//такие штуки, чтобы убирать серый текст
            resultTextBox.LostFocus += TextBox_LostFocus;//а такие, чтоб возвращать

        }
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            if (textBox != null && textBox.Text == "Задайте путь...")
            {
                textBox.Text = "";
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "Задайте путь...";
            }
        }

        private async void Window_Loaded(object sender, EventArgs e)
        {
            try
            {
                IPAddress temp = IPAddress.Parse(_host);
                _serverSocket = new Socket(temp.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                // Запускаем поток прослушивания
                listenThread = new Thread(listner);
                listenThread.IsBackground = true;
                listenThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка подключения к серверу: " + ex.Message);
                Close(); 
            }
        }

        private void AddMessage(string content, string color = "Black") // Вывод сообщений
        {
            Dispatcher.Invoke(() =>
            {
                resultTextBox.SelectionStart = resultTextBox.Text.Length;
                resultTextBox.SelectionLength = content.Length;
                resultTextBox.AppendText(content + Environment.NewLine);//добавляем сообщение в чат
                resultTextBox.ScrollToEnd(); // Прокрутка до конца
            });
        }
        public void Send(string Buffer)// Отправляем на сервер
        {
            try
            {
                // Отправка данных по UDP
                EndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(_host), _port);
                _serverSocket.SendTo(Encoding.UTF8.GetBytes(Buffer), serverEndPoint);
            }
            catch { }
        }

        private void HandleCommand(string cmd)
        {
            string[] commands = cmd.Split('#');
            foreach (string currentCommand in commands)
            {
                if (string.IsNullOrEmpty(currentCommand))
                    continue;

                try
                {
                    if (currentCommand.Contains("more"))
                    {
                        MessageBox.Show("Превышено количество запросов. Подождите, пока обработаются предыдущие.");
                        continue;
                    }
                    else if (currentCommand.Contains("msg"))
                    {
                        string[] arguments = currentCommand.Split('|');
                        if (arguments.Length >= 3)
                            AddMessage(arguments[1] + " — " + arguments[2]);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка обработки команды: " + ex.Message);
                }
            }
        }
        public async Task ListenAsync()
        {
            try
            {
                byte[] buffer = new byte[1024];
                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0); // Любой удаленный IP, порт не важен
                _serverSocket.Bind(remoteEndPoint);
                while (true)
                {
                    // Получаем данные асинхронно
                    SocketReceiveFromResult result = await _serverSocket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, remoteEndPoint);

                    int bytesReceived = result.ReceivedBytes;
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesReceived);

                    // Обработка полученных данных
                    Dispatcher.Invoke(() => HandleCommand(message));
                }
            }
            catch
            {
                MessageBox.Show("Связь с сервером прервана");
                if (Dispatcher.CheckAccess())
                {
                    // Мы уже на UI-потоке, поэтому можно закрыть форму
                    this.Close();
                }
                else
                {
                    // Не на UI-потоке, поэтому используем Dispatcher для вызова закрытия формы на UI-потоке
                    Dispatcher.Invoke(() => this.Close());
                }
            }
        }

        public void listner()
        {
            // Запускаем прослушивание асинхронно
            ListenAsync().GetAwaiter().GetResult();
        }




        private void Form_FormClosing(object sender, CancelEventArgs e) // окончание работы 
        {
            if (_serverSocket.Connected)
                Send("#endsession");
        }
        bool isFirstTime = true;
        private void SendButton_Click(object sender, EventArgs e)
        {
            string folderPath = pathTextBox.Text;

            if (string.IsNullOrWhiteSpace(folderPath) || folderPath == "Задайте путь...")
            {
                MessageBox.Show("Введите путь к папке с текстовыми файлами.");
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                MessageBox.Show("Указанной папки не существует.");
                return;
            }

            string[] txtFiles = Directory.GetFiles(folderPath, "*.txt");

            if (txtFiles.Length == 0)
            {
                MessageBox.Show("В указанной папке нет файлов с расширением .txt.");
                return;
            }
            if (isFirstTime == true)
            {
                resultTextBox.Text = "";
                isFirstTime = false;
            }
            foreach (string filePath in txtFiles)
            {
                string fileContent = File.ReadAllText(filePath);

                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    MessageBox.Show($"Файл {System.IO.Path.GetFileName(filePath)} пустой.");
                    continue;
                }

                Send($"#message|{System.IO.Path.GetFileName(filePath)}|{fileContent}"); // Отправляем содержимое файла на сервер
            }
        }
    }
}