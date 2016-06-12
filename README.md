# EasyNetwork
##Overview
A dynamic link library (DLL) written in C# which deals with all the sockets and connections to easily get you set up with a Server and Client that can communicate.

##Documentation
To get started with the library, the demo application attached is a good place to start. But in case that's not your thing, here's a summary of the classes, methods, and properties provided by this library

###EasyNetwork.Server
Provides functionality to set up a server with UDP and TCP to quickly send/receive packets, and manage connections

####connectedClients Property
Gets a list of clients as System.Net.IPEndPoint objects who have established a connection using the UDP protocol

####receivedMessages Property
Gets a list of messages as EasyNetwork.Message objects which have been received by the UDP server

####latestMessage Property
Gets the latest message as an EasyNetwork.Message object that has been received by the UDP server

####Server() Method
Initializes a new Server to be started

####Start(int port) Method
Attempt to start a server on a given port and returns true if the server was started successfully

####SendBytes(byte[] outgoing) Method
Sends a given byte[] of data over the UDP network to all clients

####SendBytes(byte[] outgoing, IPEndPoint client) Method
Sends a given byte[] of data over the UDP network to a given client

####ForceStop() Method
Forces the server to stop by aborting all active threads and clearing all data associated with it. Returns true if the server stopped successfully

###EasyNetwork.Client
Provides functionality to set up a client that can connect to a EasyNetwork.Server

####DefaultPort Static Member
The default port to be used if a client does not specify what to connect to

####receivedMessages Property
Gets a list of all messages as byte[] objects received by the UDP server

####latestMessage Property
Get the latest message as a byte[] received from the UDP server

####isConnected Property
Gets the current connection status of the client

####tcpConnectionTimeout Member
The number of seconds to wait before failing to connect the TCP client (default is 1)

####ackTimeout Member
The number of milliseconds to wait for an acknowledgement before resending a packet (default is 200)

####Client() Method
Initializes a new client to be connected

####Connect(string addr) Method
Connect the client to a given address and returns true if the connection was successful. The addr argument must be in the format of an IPV4 address (e.g. "localhost", "localhost:123", "1.2.3.4", "1.2.3.4:123"

####SendBytes(byte[] outgoing, bool reliable = false) Method
Send a given packet of data to the server over the UDP protocol. Can be made reliable, in which case the client will await an acknowledgement packet from the Server

####ForceStop() Method
Force the client to disconnect by aborting all active threads and clearing all data associated with it. Returns true if the client stopped successfully

###EasyNetwork.TcpClientStream
Stores the TCP network stream of a client with their IP address

####Stream Property
Can be read from and written to if used when constructing a StreamReader or StreamWriter

####RemoteEndPoint Property
Get the IP address and port number of the client to whom the stream belongs

####TcpClientStream(NetworkStream stream, IPEndPoint remoteEndPoint) Method
Initialize a TcpClientStream to couple a NetworkStream with the client's IPEndPoint

###EasyNetwork.Message
Represents a client's message as the data they transferred and their remote end point

####Data Property
The data as a byte[] transferred by the client over the network to the server

####RemoteEndPoint Property
Get the IP address and port number of the client by whom the data was transferred

####Message(byte[] data, IPEndPoint remoteEndPoint) Method
Initializes a new message with the given data and client address

###EasyNetwork.Reliability
Stores a byte[] to be used for reliability checking of UDP packet headers

####reliable Static Member
Represents the bytes as a byte[] to be added to the head of a UDP packet to mark as reliable

####isReliable(byte[] toCheck) Static Method
Check if the head of a given byte[] matches the reliable packet and returns true if the message is reliable

##License
* Copyright (C) 2016 David Mortiboy
* This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License, version 2 (GPL-2.0) as published by the Free Software Foundation.
* This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
* You should have received a copy of the GNU General Public License along with this program; if not, write to the Free Software Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
* If you have any further questions regarding the software, feel free to contact [David Mortiboy] (http://www.davidmortiboy.com)

* Access the [The GNU General Public License v2 (GPLv2)] (http://opensource.org/licenses/gpl-2.0) library for further reading.
