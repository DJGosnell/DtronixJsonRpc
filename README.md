# DtronixJsonRpc
DtronixJsonRpc is a fast and simple TCP RPC client and server based upon the JSON-RPC 2.0 specifications.

Currently, the application is based around this specification, but is not fully compliant. This will be the end goal of this project, but some features are skipped because they do not make sense in a TCPclient/server application.

The server and client are configurable to run in BSON or JSON modes via the JsonRpcServerConfigurations.TransportProtocol property. BSON is default because of the lower overhead of sending, parsing and dealing with byte arrays vs JSON.

### JSON-RPC 2.0 Specification Features Omitted
 - Batches. (This does not make sense in the context of a tcp client/server application.
 - Error objects. (This will be implimented, but is a low priority.
 
### Sample Server/Client
https://github.com/DJGosnell/DtronixJsonRpc/wiki/Sample-Server-Client

### License
Released under [MIT license](http://opensource.org/licenses/MIT).
