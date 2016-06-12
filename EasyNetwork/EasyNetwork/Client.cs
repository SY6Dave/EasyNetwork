/* The EasyNetwork library aims to provide a useful abstraction of the System.Net.Sockets namespace so that fast, reliable Server/Client communication may be established with little to no knowledge of the C# Sockets library.
 * Copyright (C) 2016 David Mortiboy
 *
 * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License, version 2 (GPL-2.0) as published by the Free Software Foundation.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along with this program; if not, write to the Free Software Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
 * 
 * If you have any further questions regarding the software, feel free to contact David Mortiboy via http://www.davidmortiboy.com
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace EasyNetwork
{
    /// <summary>
    /// Provides functionality to set up a client that can connect to a EasyNetwork.Server
    /// </summary>
    public class Client
    {
        public int numthreads = 0;

        private UdpClient udpClient;
        private TcpClient tcpClient;
        private List<byte[]> udpMsgs;
        private byte[] latest;
        private bool serverAckd;
        private IPEndPoint server;
        private List<Thread> activeThreads = new List<Thread>();
        private bool connected;
        private const long ALIVE_TIMEOUT = 100;

        /// <summary>
        /// The default port to be used if a client does not specify what to connect to
        /// </summary>
        public static int DefaultPort = 12345;

        /// <summary>
        /// Gets a list of all messages received by the UDP server
        /// </summary>
        public List<byte[]> receivedMessages { get { return udpMsgs; } }

        /// <summary>
        /// Get the latest message received from the UDP server
        /// </summary>
        public byte[] latestMessage { get { return latest; } }

        /// <summary>
        /// Gets the current connection status of the client
        /// </summary>
        public bool isConnected { get { return connected; } }

        /// <summary>
        /// The number of seconds to wait before failing to connect the TCP client (default is 1)
        /// </summary>
        public double tcpConnectionTimeout = 1;

        /// <summary>
        /// The number of milliseconds to wait for an acknowledgement before resending a packet (default is 200)
        /// </summary>
        public double ackTimeout = 200;

        /// <summary>
        /// Initializes a new client to be connected
        /// </summary>
        public Client()
        {
            connected = false;
            udpClient = new UdpClient();
            udpMsgs = new List<byte[]>();
            activeThreads = new List<Thread>();
            serverAckd = false;
        }

        /// <summary>
        /// Connect the client to a given address
        /// </summary>
        /// <param name="addr">IPV4 address as a string which will be parsed (can include "localhost", "localhost:123", "1.2.3.4", "1.2.3.4:123")</param>
        /// <returns>Returns true if the connection was successful</returns>
        public bool Connect(string addr)
        {
            connected = false;

            //attempt to parse the ip address
            server = ParseIP(addr);

            //start a tcp listener for the server to connect to 
            //attempt to conect the tcp client to a server
            tcpClient = new TcpClient();
            tcpClient.SendTimeout = 1000;
            tcpClient.ReceiveTimeout = 1000;
            var tryConnect = tcpClient.BeginConnect(server.Address, server.Port, null, null);
            if(!tryConnect.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(tcpConnectionTimeout)))
            {
                throw new Exception("Tcp client timed out trying to connect");
            }
            tcpClient.SendTimeout = 0;
            tcpClient.ReceiveTimeout = 0;

            //attempt to send and receive a byte to the server over the UDP protocol
            udpClient.Client.SendTimeout = 1000;
            udpClient.Client.ReceiveTimeout = 1000;
            SendHandshake();
            byte[] incoming = null;
            try
            {
                incoming = udpClient.Receive(ref server);
                connected = true;
            }
            catch
            {
                throw new Exception(String.Format("UDP connection could not be established on port {0}", server.Port));
            }

            udpClient.Client.SendTimeout = 0;
            udpClient.Client.ReceiveTimeout = 0;

            //begin threads to listen for message and keep the connection alive
            Thread udpListen = new Thread(UdpListen);
            Thread checkConnected = new Thread(CheckConnected);
            Thread keepAlive = new Thread(KeepAlive);
            keepAlive.Start();
            activeThreads.Add(keepAlive);
            udpListen.Start();
            activeThreads.Add(udpListen);
            checkConnected.Start();
            activeThreads.Add(checkConnected);
            
            return connected;
        }

        /// <summary>
        /// Send a single byte of data over the UDP protocol to establish an initial connection
        /// </summary>
        private void SendHandshake()
        {
            byte[] outgoing = new byte[] { 1 };
            udpClient.Send(outgoing, outgoing.Length, server);
        }

        /// <summary>
        /// Send a given packet of data to the server over the UDP protocol
        /// </summary>
        /// <param name="outgoing">The data to transfer</param>
        /// <param name="reliable">If the packet must be reliable, the client will await an acknowledgment to ensure delivery</param>
        public void SendBytes(byte[] outgoing, bool reliable = false)
        {
            if (connected)
            {
                if(reliable)
                {
                    //if data must be reliable, attach the reliable header and wait a specified amount of time to receive an acknowledgment
                    serverAckd = false;
                    outgoing = MakeReliable(outgoing);
                    udpClient.Send(outgoing, outgoing.Length, server);

                    Stopwatch ackTimer = new Stopwatch();
                    ackTimer.Start();

                    while (!serverAckd)
                    {
                        if (ackTimer.ElapsedMilliseconds > ackTimeout)
                        {
                            udpClient.Send(outgoing, outgoing.Length, server);
                            ackTimer.Restart();
                        }
                    }
                }
                else
                {
                    udpClient.Send(outgoing, outgoing.Length, server);
                }
            }
            else throw new Exception("Client not connected to server");
        }

        /// <summary>
        /// Forever listen for UDP messages
        /// </summary>
        private void UdpListen()
        {
            try
            {
                udpClient.BeginReceive(new AsyncCallback(UdpListenCallback), null);
            }
            catch(ThreadAbortException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }

        /// <summary>
        /// Called by the asynchronous udpClient BeginReceive in UdpListen
        /// </summary>
        /// <param name="res">Result of udpClient.BeginReceive callback</param>
        private void UdpListenCallback(IAsyncResult res)
        {
            UdpListen(); //make sure the udp server is still listening for more messages
            byte[] incoming = null;

            try
            {
                incoming = udpClient.EndReceive(res, ref server);
            }
            catch (Exception)
            {
                //if cannot receive a message, connection is now longer active so abort thread
                connected = false;
                Thread.CurrentThread.Abort();
            }

            //deal with acknowledgement packets and update the latest message
            incoming = CheckReliable(incoming);
            udpMsgs.Add(incoming);
            latest = incoming;
        }

        /// <summary>
        /// Checks the head of a message to see if it is an acknowledgement
        /// </summary>
        /// <param name="toCheck">The data to check</param>
        /// <returns>If the packet contained an acknowledgement head, this is stripped away and the remaining packet is returned</returns>
        private byte[] CheckReliable(byte[] toCheck)
        {
            if (toCheck.Length < Reliability.reliable.Length) return toCheck;

            if (Reliability.isReliable(toCheck))
            {
                serverAckd = true;

                byte[] returnArray = new byte[toCheck.Length - Reliability.reliable.Length];
                Array.Copy(toCheck, Reliability.reliable.Length, returnArray, 0, returnArray.Length);
                return returnArray;
            }
            else
            {
                return toCheck;
            }
        }

        /// <summary>
        /// Marks a packet as reliable
        /// </summary>
        /// <param name="outgoing">The data to make reliable</param>
        /// <returns>Returns the new reliable packet</returns>
        private byte[] MakeReliable(byte[] outgoing)
        {
            byte[] reliable = new byte[8 + outgoing.Length];
            for (int i = 0; i < Reliability.reliable.Length; i++) reliable[i] = Reliability.reliable[i];
            for (int i = 0; i < outgoing.Length; i++) reliable[i + Reliability.reliable.Length] = outgoing[i];
            return reliable;
        }

        /// <summary>
        /// Forever checks if the client still has a connection to  the server, and stops the listen thread if not
        /// </summary>
        private void CheckConnected()
        {
            for(;;)
            {
                if(!connected)
                {
                    ForceStop();
                }
            }
        }

        /// <summary>
        /// Periodically sends a keep alive packet via TCP to the server to check if the connection is still active
        /// </summary>
        private void KeepAlive()
        {
            Stopwatch alive = new Stopwatch();
            alive.Start();
            for (; ; )
            {
                if (alive.ElapsedMilliseconds > ALIVE_TIMEOUT)
                {
                    alive.Restart();
                    //Attempt to flush a byte of data over the network stream
                    SendHandshake();
                }
            }
        }

        /// <summary>
        /// Force the client to disconnect by aborting all active threads and clearing all data associated with it
        /// </summary>
        /// <returns></returns>
        public bool ForceStop()
        {
            Stopwatch threadStopper = new Stopwatch();
            threadStopper.Start();
            bool allStopped = false;

            while (!allStopped)
            {
                for (int i = 0; i < activeThreads.Count; )
                {
                    while (activeThreads[i].IsAlive)
                    {
                        if (threadStopper.ElapsedMilliseconds > 100)
                        {
                            threadStopper.Restart();
                            activeThreads[i].Abort();
                        }
                    }
                    activeThreads.RemoveAt(i);

                    if (activeThreads.Count == 0) allStopped = true;
                    break;
                }
                if (activeThreads.Count == 0) allStopped = true;
            }

            connected = false;
            tcpClient.Client.Close(1);
            udpClient = new UdpClient();
            udpMsgs.Clear();
            serverAckd = false;

            return true;
        }

        #region IP Address Parsing

        /*
         * Adapted from Mitch at StackOverflow
         * http://stackoverflow.com/a/12044845/3005539
         */

        private IPEndPoint ParseIP(string endPoint)
        {
            return ParseIP(endPoint, -1);
        }

        private IPEndPoint ParseIP(string endPoint, int defaultPort)
        {
            if (string.IsNullOrEmpty(endPoint) || endPoint.Trim().Length == 0)
            {
                throw new ArgumentException("IP address may not be empty");
            }

            if (defaultPort != -1 && (defaultPort < IPEndPoint.MinPort || defaultPort > IPEndPoint.MaxPort))
            {
                throw new ArgumentException(string.Format("Invalid port '{0}'", defaultPort));
            }

            string[] values = endPoint.Split(new char[] { ':' });
            IPAddress addr;
            int port = -1;

            if (values.Length <= 2) //ipv4 or hostname
            {
                if (values.Length == 1)
                    //no port specified, default
                    port = defaultPort;
                else
                    port = GetPort(values[1]);

                //try to use address as IPv4, otherwise get hostname
                if (!IPAddress.TryParse(values[0], out addr))
                    addr = getIPfromHost(values[0]);
            }
            else
            {
                throw new FormatException(string.Format("Invalid endpoint ipaddress '{0}'", endPoint));
            }

            if (port == -1)
                port = DefaultPort;

            return new IPEndPoint(addr, port);
        }

        private int GetPort(string p)
        {
            int port;

            if (!int.TryParse(p, out port)
         || port < IPEndPoint.MinPort
         || port > IPEndPoint.MaxPort)
            {
                throw new FormatException(string.Format("Invalid end point port '{0}'", p));
            }

            return port;
        }

        private IPAddress getIPfromHost(string p)
        {
            var hosts = Dns.GetHostAddresses(p);

            if (hosts == null || hosts.Length == 0)
                throw new ArgumentException(string.Format("Host not found: {0}", p));

            for (int i = 0; i < hosts.Length; i++)
            {
                if (hosts[i].AddressFamily == AddressFamily.InterNetwork) return hosts[i];
            }

            throw new ArgumentException(string.Format("Only ipv4 addresses are supported at this time."));
        }

        #endregion
    }
}
