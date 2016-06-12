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
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace EasyNetwork
{
    /// <summary>
    /// Stores the TCP network stream of a client with their IP address
    /// </summary>
    public struct TcpClientStream
    {
        private NetworkStream stream;
        private IPEndPoint remoteEndPoint;

        /// <summary>
        /// Can be read from and written to using a StreamReader or StreamWriter
        /// </summary>
        public NetworkStream Stream { get { return stream; } }

        /// <summary>
        /// The IP address and port number of the client to whom the stream belongs
        /// </summary>
        public IPEndPoint RemoteEndPoint { get { return remoteEndPoint; } }

        /// <summary>
        /// Initialize a TcpClientStream to couple a NetworkStream with the client's IPEndPoint
        /// </summary>
        /// <param name="stream">The NetworkStream of the client</param>
        /// <param name="remoteEndPoint">The address of the client as an IPEndPoint</param>
        public TcpClientStream(NetworkStream stream, IPEndPoint remoteEndPoint)
        {
            this.stream = stream;
            this.remoteEndPoint = remoteEndPoint;
        }
    }

    /// <summary>
    /// Represents a client's message as the data they transferred and their remote end point
    /// </summary>
    public struct Message
    {
        private byte[] data;
        private IPEndPoint remoteEndPoint;

        /// <summary>
        /// The data transferred by the client over the network to the server
        /// </summary>
        public byte[] Data { get { return data; } }

        /// <summary>
        /// The IP address and port number of the client by whom the data was transferred
        /// </summary>
        public IPEndPoint RemoteEndPoint { get { return remoteEndPoint; } }

        /// <summary>
        /// Initializes a new message with the given data and client address
        /// </summary>
        /// <param name="data">The data transferred by the client</param>
        /// <param name="remoteEndPoint">The address of the client as an IPEndPoint</param>
        public Message(byte[] data, IPEndPoint remoteEndPoint)
        {
            this.data = data;
            this.remoteEndPoint = remoteEndPoint;
        }
    }

    /// <summary>
    /// Provides functionality to set up a server with UDP and TCP to quickly send/receive packets, and manage connections
    /// </summary>
    public class Server
    {
        private UdpClient udpServer;
        private List<IPEndPoint> udpClients;
        private Dictionary<IPEndPoint, bool> udpClientAcks;
        private List<Message> udpMsgs;
        private Message latest;
        private TcpListener tcpServer;
        private List<TcpClientStream> tcpClientStreams;
        private List<Thread> activeThreads;
        private int port;
        private const long ALIVE_TIMEOUT = 100;

        /// <summary>
        /// A list of clients who have established a connection using the UDP protocol
        /// </summary>
        public List<IPEndPoint> connectedClients { get { return udpClients; } }

        /// <summary>
        /// Gets a list of all messages received by the UDP server
        /// </summary>
        public List<Message> receivedMessages { get { return udpMsgs; } }

        /// <summary>
        /// Get the latest message received by the UDP server
        /// </summary>
        public Message latestMessage { get { return latest; } }
        
        /// <summary>
        /// Initialize a new server to be started
        /// </summary>
        public Server()
        {
            udpClients = new List<IPEndPoint>();
            udpClientAcks = new Dictionary<IPEndPoint, bool>();
            udpMsgs = new List<Message>();
            tcpClientStreams = new List<TcpClientStream>();
            activeThreads = new List<Thread>();
        }

        /// <summary>
        /// Attempt to start a server on a given port
        /// </summary>
        /// <param name="port">Port must be opened on TCP and UDP for your machine on your network</param>
        /// <returns>Returns true if the server was started successfully</returns>
        public bool Start(int port)
        {
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort) return false;
            this.port = port;
            udpServer = new UdpClient(port);
            tcpServer = new TcpListener(IPAddress.Any, port);
            tcpServer.Start();

            //begin a series of threads to listen for messages and keep the connection active
            Thread udpListener = new Thread(UdpListen);
            Thread tcpListener = new Thread(TcpListen);
            Thread keepAlive = new Thread(KeepAlive);
            keepAlive.Start();
            activeThreads.Add(keepAlive);
            udpListener.Start();
            activeThreads.Add(udpListener);
            tcpListener.Start();
            activeThreads.Add(tcpListener);

            return true;
        }

        /// <summary>
        /// Forever listens for UDP messages
        /// </summary>
        private void UdpListen()
        {
            try
            {
                udpServer.BeginReceive(new AsyncCallback(UdpListenCallback), null);
            }
            catch (ThreadAbortException)
            {
                return;
            }
            catch(ObjectDisposedException)
            {
                return;
            }
        }

        /// <summary>
        /// Called by the asynchronous udpServer BeginReceive in UdpListen
        /// </summary>
        /// <param name="res">Result of udpServer.BeginReceive callback</param>
        private void UdpListenCallback(IAsyncResult res)
        {
            UdpListen(); //make sure the udp server is still listening for more messages
            IPEndPoint client = new IPEndPoint(IPAddress.Any, port);
            byte[] incoming = null;
            try
            {
                incoming = udpServer.EndReceive(res, ref client);
            }
            catch
            {
                //client has probably disconnected but this gets dealt with by keep alive
                return;
            }

            //If a new client has connected, add them to the list and send a handshake response
            if (!udpClients.Contains(client))
            {
                udpClients.Add(client);
                SendBytes(new byte[] { 1 }, client);
            }
            else
            {
                if (!(incoming.Length == 1 && incoming[0] == 1)) //not keep alive handshake
                {
                    //if this is an already connected client, construct a message and update the latest
                    incoming = CheckReliable(incoming, client);
                    Message msg = new Message(incoming, client);
                    udpMsgs.Add(msg);
                    latest = msg;
                }
            }
        }

        /// <summary>
        /// Forever listens for TCP connections
        /// </summary>
        private void TcpListen()
        {
            try
            {
                for (; ; )
                {
                    if (tcpServer.Pending())
                    {
                        Socket client = tcpServer.AcceptSocket();
                        NetworkStream stream = new NetworkStream(client);
                        tcpClientStreams.Add(new TcpClientStream(stream, (IPEndPoint)client.RemoteEndPoint));
                    }
                    else Thread.Sleep(10);
                }
            }
            catch (ThreadAbortException)
            {
                return;
            }
        }

        /// <summary>
        /// Periodically sends a keep alive packet via TCP to each client to check if their connections are still active
        /// </summary>
        private void KeepAlive()
        {
            Stopwatch alive = new Stopwatch();
            alive.Start();

            try
            {
                for (; ; )
                {
                    if (udpClients.Count == 0) continue;
                    if (alive.ElapsedMilliseconds > ALIVE_TIMEOUT)
                    {
                        alive.Restart();
                        for (int i = 0; i < tcpClientStreams.Count; i++)
                        {
                            //for each TCP client, attempt to flush a byte of data over their network stream
                            StreamWriter clientWriter = new StreamWriter(tcpClientStreams[i].Stream);
                            clientWriter.WriteLine(1);
                            try
                            {
                                clientWriter.Flush();
                            }
                            catch (ThreadAbortException)
                            {
                                return;
                            }
                            catch
                            {
                                //if data could not be flushed, the connection is no longer active so remove them
                                tcpClientStreams[i].Stream.Close();
                                tcpClientStreams.RemoveAt(i);
                                udpClients.RemoveAt(i);
                            }
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {
                return;
            }
        }

        /// <summary>
        /// Sends a given byte[] of data over the UDP network to all clients
        /// </summary>
        /// <param name="outgoing">Data to be transferred</param>
        public void SendBytes(byte[] outgoing)
        {
            for (int i = 0; i < udpClients.Count; i++)
            {
                udpServer.Send(outgoing, outgoing.Length, udpClients[i]);
            }
        }

        /// <summary>
        /// Sends a given byte[] of data over the UDP network to a given client
        /// </summary>
        /// <param name="outgoing">dDta to be transferred</param>
        /// <param name="client">Remote end point of the client to transfer data to</param>
        public void SendBytes(byte[] outgoing, IPEndPoint client)
        {
            udpServer.Send(outgoing, outgoing.Length, client);
        }

        /// <summary>
        /// Checks the head of a packet and acknowledges if required
        /// </summary>
        /// <param name="toCheck">The data packet to check</param>
        /// <param name="client">The client who sent the packet</param>
        /// <returns>If the packet contained an acknowledgement head, this is stripped away and the remaining packet is returned</returns>
        private byte[] CheckReliable(byte[] toCheck, IPEndPoint client)
        {
            if (toCheck.Length < Reliability.reliable.Length) return toCheck;

            if (Reliability.isReliable(toCheck))
            {
                //if the head matches the reliable packet, acknowledge the message to the client
                SendBytes(toCheck, client);

                //return the packet without the head
                byte[] returnArray = new byte[toCheck.Length - Reliability.reliable.Length];
                Array.Copy(toCheck, Reliability.reliable.Length, returnArray, 0, returnArray.Length);
                return returnArray;
            }
            else
            {
                //packet is not reliable so return as is
                return toCheck;
            }
        }

        /// <summary>
        /// Force the server to stop by aborting all active threads and clearing all data associated with it
        /// </summary>
        /// <returns>Returns true if the server stopped successfully</returns>
        public bool ForceStop()
        {
            Stopwatch threadStopper = new Stopwatch();
            threadStopper.Start();
            bool allStopped = false;

            while(!allStopped)
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
            }

            udpClients.Clear();
            udpClientAcks.Clear();
            udpMsgs.Clear();
            tcpClientStreams.Clear();
            udpServer.Close();
            tcpServer.Stop();

            return true;
        }
    }

    /// <summary>
    /// Stores a byte[] to be used for reliability checking of UDP packet headers
    /// </summary>
    public static class Reliability
    {
        /// <summary>
        /// Bytes to be added to the head of a UDP packet to mark as reliable
        /// </summary>
        public static byte[] reliable = Encoding.ASCII.GetBytes("+8WFuPS0Q/y5jojHSqDugbTRKz6n97BvOlxC1xK/RvSakg5MpzM4qoJaKVoUleid");

        /// <summary>
        /// Check if the head of a given byte[] matches the reliable packet
        /// </summary>
        /// <param name="toCheck">Data to check</param>
        /// <returns>Returns true if the message is reliable and false if not</returns>
        public static bool isReliable(byte[] toCheck)
        {
            bool rel = true;
            for(int i = 0; i < reliable.Length; i++)
            {
                if(toCheck[i] != reliable[i])
                {
                    rel = false;
                    break;
                }
            }
            return rel;
        }
    }
}