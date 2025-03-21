# ClientServer
Клиентское и серверное приложение с использованием tcp и udp.

## Сервер

Консольное приложение.

При запуске сервера задается параметр N — максимальное количество одновременно обрабатываемых запросов.

Основная задача сервера — обработка запросов клиентов. Тело запроса представляет собой текстовую строку, а клиент ожидает ответа, является ли эта строка палиндромом.

Сервер может одновременно обрабатывать запросы от множества клиентов, но из-за ограниченных ресурсов одновременно он может обрабатывать только N запросов (именно запросов, а не клиентов). Если количество запросов превышает N, сервер немедленно вернет ошибку.

Для имитации перегрузки сервера обработка каждого запроса занимает не менее 1 секунды. 

## Клиент

Графическое приложение WPF.

При запуске клиент принимает путь к папке с входными данными. Входные данные представляют собой текстовые файлы произвольной длины.

Основная задача клиента — отправить содержимое каждого файла на сервер и отобразить ответ сервера для каждого файла. Таким образом, по завершении работы клиента будет получен корректный ответ для каждого файла, является ли его содержимое палиндромом. Один файл соответствует одному запросу.

Клиент не знает, сколько запросов может одновременно обрабатывать сервер, но стремиться использовать сервер максимально эффективно. (То есть даже один клиент с большим количеством файлов может вызвать перегрузку сервера). Клиент обрабатывает все файлы из указанной папки в любом случае.
