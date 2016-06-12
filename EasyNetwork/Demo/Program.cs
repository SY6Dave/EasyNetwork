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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EasyNetwork;
using System.Threading;

namespace Demo
{
    /// <summary>
    /// A sample application to be shipped with the EasyNetwork library to demonstrate how to use, and what it can be used for
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            string response = "yes";
            while(response == "yes")
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("This sample application will demonstrate the capabilities\nof this library to easily get set up with a Client and Server");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.WriteLine("The EasyNetwork library uses TCP and UDP sockets to get the best\nof both speed and reliability when transferring data across the network");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.WriteLine("Call the .Start(int port) method on an instance of Server to\nbegin listening for Clients. Note that the port must be forwarded on\nTCP & UDP on your network");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                Server server = new Server();
                server.Start(123); Console.WriteLine("Server started");

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();

                Console.WriteLine("Call the .Connect(string addr) method on a Client instance to\nattempt to connect to a server. The addr parameter may be in any IPV4 format\ne.g.: \"localhost\", \"localhost:123\", \"1.2.3.4\", \"1.2.3.4:123\"");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                Client client = new Client();
                if (client.Connect("localhost:123"))
                    Console.WriteLine("Client connected to Server");

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();

                Console.WriteLine("The .SendBytes(byte[] outgoing, bool reliable = false) method on the Client\ncan be used to send any given array of bytes to the server over UDP.");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                Console.Write("Send an unreliable message to the Server: ");
                Console.ForegroundColor = ConsoleColor.Green;
                client.SendBytes(Encoding.ASCII.GetBytes(Console.ReadLine()));

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("---------------------------------------------------------------");

                Console.WriteLine("The Server has some pretty useful properties. .latestMessage returns\nthe most recently received byte[] and the address of the Client sender");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                Thread.Sleep(100);
                Console.WriteLine("Server's most recently received message is: {0}", Encoding.ASCII.GetString(server.latestMessage.Data));
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();

                Console.WriteLine("Back to the Client, the reliable argument of .SendBytes(...) specifies\nwhether or not to use acknowledgement packets to ensure delivery");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                Console.Write("Send a reliable message to the server: ");
                Console.ForegroundColor = ConsoleColor.Green;
                client.SendBytes(Encoding.ASCII.GetBytes(Console.ReadLine()));
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("---------------------------------------------------------------");

                Thread.Sleep(10);

                Console.WriteLine("As well as .latestMessage, a list of all Server messages can be got with\n.receivedMessages");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;


                Console.WriteLine("All Server messages are:");
                foreach (Message m in server.receivedMessages) Console.WriteLine(Encoding.ASCII.GetString(m.Data));
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();

                Console.WriteLine("The Server can send a message to an individual Client using the\n.SendBytes(byte[] outgoing, IPEndPoint client) method");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                Console.Write("Send a Server message to the Client: ");
                Console.ForegroundColor = ConsoleColor.Green;
                server.SendBytes(Encoding.ASCII.GetBytes(Console.ReadLine()), server.connectedClients[0]);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("---------------------------------------------------------------");

                Thread.Sleep(10);

                Console.WriteLine("Similar to the Server, the Client can retrieve its latest message with the\n.latestMessage property");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                Console.WriteLine("Client's most recently received message is: {0}", Encoding.ASCII.GetString(client.latestMessage));
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();

                Console.WriteLine("If multiple Clients are connected, the Server can send a message to all of\nthem using .SendBytes(byte[] outgoing)");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                Console.Write("Send a Server message to all Clients: ");
                Console.ForegroundColor = ConsoleColor.Green;
                server.SendBytes(Encoding.ASCII.GetBytes(Console.ReadLine()));
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("---------------------------------------------------------------");

                Thread.Sleep(10);

                Console.WriteLine("Similar again to the Server, the Client can retrieve all messages\nwith .receivedMessages property");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                Console.WriteLine("All Client messages are:");
                foreach (Byte[] m in client.receivedMessages) Console.WriteLine(Encoding.ASCII.GetString(m));
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();

                Console.WriteLine("To force a Server to disconnect, the .ForceStop() method can be called.\nThis terminates all associated threads and disconnects all Clients");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                Console.WriteLine("Now disconnecting server...");
                server.ForceStop();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("---------------------------------------------------------------");

                Console.WriteLine("A Client's connection status can be got with the .isConnected property");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                Console.WriteLine("Client connection status is: {0}", client.isConnected);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();

                Console.WriteLine("The Client can also be disconnected with the .ForceStop() method");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                client.ForceStop();
                server.Start(123); Console.WriteLine("Restarted Server...");

                client.Connect("localhost:123");
                Console.WriteLine("Re-connected Client...");

                Console.WriteLine("Now disconnecting Client with .ForceStop()...");
                client.ForceStop();

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();

                Console.WriteLine("A list of all the Server's currently connected Clients can be got with\nthe .connectedClients property");
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.DarkCyan;

                //Thread.Sleep(250); //keepalive are sent every 100ms so make sure the server has a chance to notice it's disconnected
                Console.WriteLine("Number of Clients connected to server: {0}", server.connectedClients.Count);
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("---------------------------------------------------------------");
                Console.ReadLine();

                server.ForceStop();
                Console.WriteLine();
                Console.WriteLine("That's just about all the most important features to get set up with the\nEasyNetwork library. Other properties exist and can be changed such as\nconnection timeouts and default ports - all of which are fully commented\nand hopefully self-explanatory!");
                Console.WriteLine("---------------------------------------------------------------");
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine();
                Console.WriteLine("Type yes to restart demo or press enter to terminate");
                Console.ForegroundColor = ConsoleColor.Green;
                response = Console.ReadLine();
            }
        }
    }
}
