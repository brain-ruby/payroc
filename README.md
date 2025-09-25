# Payroc - Technical Interview Exercise

## Description

This project implements a basic, software-based load-balancer, operating at layer 4 (TCP only).

`Tests.cs` demos how the application relays requests from clients to target servers.

## Build and test

Target framework: **.NET 8**

Dependencies: **xUnit, WireMock.NET and FluentAssertions**

To build and execute the tests, run:

```bash
dotnet build
dotnet test
```

## Design

The load balancer opens a new `Socket` on startup to listen for TCP connections. When a connection is recieved, a new client `Socket` is created to handle the incoming request, and another `Socket` is opened to the next available target endpoint.

Two connection relays are then setup up and run simultaneously: from client -> target server, and from target server -> client, relaying the bytes between each.

The load balancer signals to the 'to' connection that sending has finished once the end of field marker is received from the 'from' connection. This enables handling keep-alive connections e.g. from HTTP clients.

# Assumptions
- The solution needed to be put together rapidly to solve the immediate problem.
- As it's '1999', some modern constructs (e.g. `HostBuilder`) have been avoided.

# Limitations and omissions
- There's no logging
- There's only basic error handling
- If all target servers become unreachable, the load balancer will crash
- Target servers are removed upon _any_ connection problem - transient or otherwise
- Target servers are never re-added to the list of active servers
- The target server DNS is resolved each time a connection is set up
- A new connection to a target server is created for every client connection
- A single strategy - round robin - is used to balance across targets
- Only handles TCP connections (e.g. not UDP)
- There's no handling of connection timeouts or graceful shutdown
