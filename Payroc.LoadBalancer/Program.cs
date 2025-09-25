using System.Net;
using System.Net.Sockets;

namespace Payroc.LoadBalancer;

public static class Program
{
    public static async Task Main(string[] args)
    {
        _targetEndpoints = new Queue<Endpoint>(ParseTargetEndpoints(args));
        
        using var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, 8080));

        Console.WriteLine($"Listening on {listenSocket.LocalEndPoint}");

        listenSocket.Listen();

        while (true)
        {
            var clientSocket = await listenSocket.AcceptAsync();

            _ = Task.Run(() => ProcessClientConnection(clientSocket));
        }
    }

    private static IEnumerable<Endpoint> ParseTargetEndpoints(string[] args)
    {
        var targetServerArg = args[0];
        var entries = targetServerArg.Split(";");

        return entries.Select(e =>
        {
            var parts = e.Split(":");
            
            return new Endpoint(
                Hostname: parts[0], Port: int.Parse(parts[1]));
        });
    }

    private static async Task? ProcessClientConnection(Socket clientSocket)
    {
        try
        {
            using var targetSocket = await ConnectToTarget();

            var clientToTarget = RelayAsync(from: clientSocket, to: targetSocket);
            var targetToClient = RelayAsync(from: targetSocket, to: clientSocket);

            await Task.WhenAll(clientToTarget, targetToClient);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Relay error: {ex.Message}");
        }
        finally
        {
            clientSocket.Dispose();
        }
    }

    private static async Task RelayAsync(Socket from, Socket to)
    {
        var buffer = new byte[4 * 1024];

        while (true)
        {
            int received = await from.ReceiveAsync(buffer, SocketFlags.None);

            if (received == 0)
            {
                break;
            }

            int sent = 0;

            while (sent < received)
            {
                sent += await to.SendAsync(
                    new ArraySegment<byte>(buffer, sent, received - sent), SocketFlags.None);
            }
        }

        to.Shutdown(SocketShutdown.Send);
    }

    private static async Task<Socket> ConnectToTarget()
    {
        Socket? targetSocket = null;
        try
        {
            targetSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            do
            {
                var endpoint = GetNextEndpoint();
                var host = await Dns.GetHostEntryAsync(endpoint.Hostname);
                    
                try
                {
                    await targetSocket.ConnectAsync(new IPEndPoint(host.AddressList[0], endpoint.Port));
                    break;
                }
                catch (SocketException)
                {
                    BlacklistEndpoint(endpoint);
                }
            } while (_targetEndpoints.Count != 0);

            return targetSocket;
        }
        catch
        {
            targetSocket?.Dispose();
            throw;
        }
    }

    private record Endpoint(string Hostname, int Port);

    private static Queue<Endpoint> _targetEndpoints = new();
    
    private static Endpoint GetNextEndpoint()
    {
        var next = _targetEndpoints.Dequeue();
        
        _targetEndpoints.Enqueue(next);
        
        return next;
    }
    
    private static void BlacklistEndpoint(Endpoint endpoint)
    {
        _targetEndpoints = new Queue<Endpoint>(_targetEndpoints.Except([endpoint]));
    }
}