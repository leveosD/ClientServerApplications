# Client-Server Subscription System

This repository contains laboratory works for the **"Client-Server Application Development"** course. Each project implements a publish-subscribe system where users subscribe to specific topics to receive targeted messages.

## Topic Hierarchy & Routing Logic

When sending a message, the user specifies a destination topic. The message is routed only to users subscribed to that specific topic or to any of its parent (higher-level) topics.

### Example Scenario:
* **User 1** sends the message `"hello!"` to the topic `aaa.bbb.ccc`.
* **User 2** **receives** the message because they are subscribed directly to `aaa.bbb.ccc`.
* **User 3** **receives** the message because they are subscribed to the parent topic `aaa.bbb`.
* **User 4** **does not receive** the message because they are only subscribed to `bbb`.

## Projects Breakdown

The system is implemented across three different lab works, each utilizing a distinct communication protocol:

* **Lab 1: TCP Implementation** — Reliable, connection-oriented data transfer.
* **Lab 2: UDP Implementation** — Fast, connectionless datagram streaming.
* **Lab 3: REST API Implementation** — HTTP-based request-response architecture.
