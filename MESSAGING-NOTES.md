# Messaging with RabbitMQ: What I Learned

## Design
- Orders saves Pending, publishes order.placed, returns 202
- Products consumes, reduces stock, publishes stock.reserved / stock.rejected
- Orders consumes the result and sets Confirmed / Rejected

## Reliability settings
- Durable exchange and queues, persistent messages, manual ack after save, prefetch 1

## Test results
(paste your results table)

## What got better
- ...
## What got harder
- ...
## Known gaps
- Outbox pattern, dead-letter queue, publisher confirms, auto-reconnect, correlation IDs

## Issues I hit and fixes
- Wrong Docker port mapping (5672:15671) -> re-ran container with 5672:5672
- Identity column on ProcessedMessages -> ValueGeneratedNever() + database reset
- Product not found -> tolerant JSON deserialization
- EnsureCreated does not update schemas -> delete database (Migrations later)



3. Self-check questions (answer in your own words)
Why does the Orders consumer update only Pending orders?
What happens to a result message if Orders is down?
How did the system survive Products being down, compared with the HTTP design in Task 1?
How did the duplicate message get handled, and which table made it possible?
What is lost when RabbitMQ is down while an order is placed, and which pattern fixes it?
What is a poison message, and how would a dead-letter queue help?