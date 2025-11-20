This repo provides the components needed to build a communications network to allow users who have a set of common pre-shared keys to communicate:
a) without identifying themselves
b) without identifying themselves to each other and
c) without the network traffic linking them to each other

There are two branches in thsi repo which work differently and will never be fully merged:
a) master in which the Signalr hubs transform and reroute them and are responsible for banning miscreants
b) keyless in which the Signalr hubs pass messages directly through and client nodes ban miscreants

Both use projects from the https://github.com/flyhull/Encryption-Toolkit repo for cryption and obsfucation

Since Signalr is used as the transport layer for encrypted messages embedded in PNG images, the largest practical plaintext message size is 8K.

Since some Mime Detective definition sets require a license for commercial use, the free default definitions are used. 
