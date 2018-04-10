﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebApiClient
{
    /// <summary>
    /// 提供代理的验证
    /// </summary>
    public static class ProxyValidator
    {
        /// <summary>
        /// 使用http tunnel检测代理状态
        /// </summary>
        /// <param name="proxyHost">代理服务器域名或ip</param>
        /// <param name="proxyPort">代理服务器端口</param>
        /// <param name="targetAddress">目标url地址</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <returns></returns>
        public static HttpStatusCode Validate(string proxyHost, int proxyPort, Uri targetAddress)
        {
            return Validate(proxyHost, proxyPort, null, null, targetAddress);
        }

        /// <summary>
        /// 使用http tunnel检测代理状态
        /// </summary>
        /// <param name="proxyHost">代理服务器域名或ip</param>
        /// <param name="proxyPort">代理服务器端口</param>
        /// <param name="userName">代理账号</param>
        /// <param name="password">代理密码</param>
        /// <param name="targetAddress">目标url地址</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <returns></returns>
        public static HttpStatusCode Validate(string proxyHost, int proxyPort, string userName, string password, Uri targetAddress)
        {
            var remoteEndPoint = new DnsEndPoint(proxyHost, proxyPort, AddressFamily.InterNetwork);
            var socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                socket.SendTimeout = 3 * 1000;
                socket.ReceiveTimeout = 5 * 1000;
                socket.Connect(remoteEndPoint);

                var request = BuildHttpTunnelRequestString(proxyHost, proxyPort, userName, password, targetAddress);
                var sendBuffer = Encoding.ASCII.GetBytes(request);
                socket.Send(sendBuffer);

                var recvBuffer = new byte[150];
                var length = socket.Receive(recvBuffer);

                var response = Encoding.ASCII.GetString(recvBuffer, 0, length);
                var statusCode = int.Parse(Regex.Match(response, "(?<=HTTP/1.1 )\\d+", RegexOptions.IgnoreCase).Value);
                return (HttpStatusCode)statusCode;
            }
            catch (Exception)
            {
                return HttpStatusCode.ServiceUnavailable;
            }
            finally
            {
                socket.Dispose();
            }
        }

        /// <summary>
        /// 使用http tunnel检测代理状态
        /// </summary>
        /// <param name="proxyHost">代理服务器域名或ip</param>
        /// <param name="proxyPort">代理服务器端口</param>
        /// <param name="targetAddress">目标url地址</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <returns></returns>
        public static async Task<HttpStatusCode> ValidateAsync(string proxyHost, int proxyPort, Uri targetAddress)
        {
            return await ValidateAsync(proxyHost, proxyPort, null, null, targetAddress);
        }

        /// <summary>
        /// 使用http tunnel检测代理状态
        /// </summary>
        /// <param name="proxyHost">代理服务器域名或ip</param>
        /// <param name="proxyPort">代理服务器端口</param>
        /// <param name="userName">代理账号</param>
        /// <param name="password">代理密码</param>
        /// <param name="targetAddress">目标url地址</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <returns></returns>
        public static async Task<HttpStatusCode> ValidateAsync(string proxyHost, int proxyPort, string userName, string password, Uri targetAddress)
        {
            var remoteEndPoint = new DnsEndPoint(proxyHost, proxyPort, AddressFamily.InterNetwork);
            var socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                socket.SendTimeout = 3 * 1000;
                socket.ReceiveTimeout = 5 * 1000;
                await Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, remoteEndPoint, null);

                var request = BuildHttpTunnelRequestString(proxyHost, proxyPort, userName, password, targetAddress);
                var sendBuffer = Encoding.ASCII.GetBytes(request);
                var sendArraySegment = new List<ArraySegment<byte>> { new ArraySegment<byte>(sendBuffer) };
                await Task.Factory.FromAsync(socket.BeginSend, socket.EndSend, sendArraySegment, SocketFlags.None, null);

                var recvArraySegment = new List<ArraySegment<byte>> { new ArraySegment<byte>(new byte[150]) };
                var length = await Task.Factory.FromAsync(socket.BeginReceive, socket.EndReceive, recvArraySegment, SocketFlags.None, null);

                var response = Encoding.ASCII.GetString(recvArraySegment[0].Array, 0, length);
                var statusCode = int.Parse(Regex.Match(response, "(?<=HTTP/1.1 )\\d+", RegexOptions.IgnoreCase).Value);
                return (HttpStatusCode)statusCode;
            }
            catch (Exception)
            {
                return HttpStatusCode.ServiceUnavailable;
            }
            finally
            {
                socket.Dispose();
            }
        }


        /// <summary>
        /// 生成Http Tunnel请求字符串
        /// </summary>
        /// <param name="proxyHost">代理服务器域名或ip</param>
        /// <param name="proxyPort">代理服务器端口</param>
        /// <param name="userName">代理账号</param>
        /// <param name="password">代理密码</param>
        /// <param name="targetAddress">目标url地址</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns></returns>
        private static string BuildHttpTunnelRequestString(string proxyHost, int proxyPort, string userName, string password, Uri targetAddress)
        {
            if (proxyHost == null)
            {
                throw new ArgumentNullException(nameof(proxyHost));
            }

            if (targetAddress == null)
            {
                throw new ArgumentNullException(nameof(targetAddress));
            }

            const string CRLF = "\r\n";
            var builder = new StringBuilder()
                .Append($"CONNECT {targetAddress.Host}:{targetAddress.Port} HTTP/1.1{CRLF}")
                .Append($"Host: {targetAddress.Host}:{targetAddress.Port}{CRLF}")
                .Append($"Accept: */*{CRLF}")
                .Append($"Content-Type: text/html{CRLF}")
                .Append($"Proxy-Connection: Keep-Alive{CRLF}")
                .Append($"Content-length: 0{CRLF}");

            if (userName != null && password != null)
            {
                var bytes = Encoding.ASCII.GetBytes($"{userName}:{password}");
                var base64 = Convert.ToBase64String(bytes);
                builder.AppendLine($"Proxy-Authorization: Basic {base64}{CRLF}");
            }

            return builder.Append(CRLF).ToString();
        }
    }
}
