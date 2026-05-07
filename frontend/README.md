# StockOrchestra-V2

This is the frontend component of the StockOrchestra ecosystem, a real-time, event-driven financial data orchestration platform. The project is bootstrapped with [`create-next-app`](https://nextjs.org/docs/app/api-reference/cli/create-next-app) and serves as the real-time dashboard connecting to our .NET Core microservices.

## Architecture Overview

StockOrchestra-V2 is built on a loosely coupled microservices architecture designed for high availability and low latency:

- Frontend: Next.js
- Backend: .NET Core Microservices (Price Discovery, Analytical Store, etc.)
- Message Broker: Redis Streams (Pub/Sub for real-time data pipelines)
- Database: TimescaleDB (for time-series analytical data)
- Infrastructure: Docker & Kubernetes
- Next Level (Roadmap): Autonomous AI Agents via Model Context Protocol (MCP)

## Getting Started

### Prerequisites

Since this frontend relies on real-time data streaming, ensure that the backend ecosystem (Redis, .NET Microservices, and SignalR Hub) is running via Docker before starting the Next.js application.

To start the backend infrastructure, navigate to the root directory and run:

```bash
docker-compose up --build -d
Running the Frontend Locally
Once the backend ecosystem is up and running, start the Next.js development server:

Bash
npm run dev
# or
yarn dev
# or
pnpm dev
# or
bun dev
Open http://localhost:3000 with your browser to see the real-time dashboard in action.

You can start editing the page by modifying app/page.tsx. The page auto-updates as you edit the file.

Learn More
To learn more about Next.js, take a look at the following resources:

Next.js Documentation - learn about Next.js features and API.

Learn Next.js - an interactive Next.js tutorial.

You can check out the Next.js GitHub repository - your feedback and contributions are welcome!

Deploy on Vercel
The easiest way to deploy your Next.js app is to use the Vercel Platform from the creators of Next.js.

Check out our Next.js deployment documentation for more details.
```
