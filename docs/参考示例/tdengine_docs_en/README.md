# TDengine TSDB Documentation (English)

This directory contains the English documentation for TDengine TSDB, a time-series database purpose-built for Industry 4.0 and Industrial IoT.

## Directory Structure

### 01 - Documentation Home
- **01-index.md** - Documentation homepage, overview of TDengine TSDB

### 03 - Introduction
- **03-intro.md** - Introduction to TDengine TSDB, including product offerings (OSS, Enterprise, Cloud), core competencies, and application scenarios

### 04 - Get Started
Quick setup guide for TDengine environment.

| File | Description |
|------|-------------|
| index.md | Section overview |
| 01-docker.md | Docker deployment |
| 03-package.md | Package installation |
| 05-cloud.md | TDengine Cloud setup |

### 05 - Basic Features
Core data operations of TDengine.

| File | Description |
|------|-------------|
| index.md | Section overview |
| 01-model.md | Data model (supertables, subtables, tags) |
| 02-insert.md | Data insertion methods |
| 03-query.md | SQL query syntax and functions |

### 06 - Advanced Features
Advanced capabilities beyond basic operations.

#### 06-advanced/01-subscription - Data Subscription
| File | Description |
|------|-------------|
| index.md | Overview |
| 01-topic.md | Topic management |
| 02-native.md | Native subscription API |
| 03-mqtt.md | MQTT subscription |

#### 06-advanced/02-cache.md - Read Caching
FIFO cache management for real-time data access.

#### 06-advanced/03-stream.md - Stream Processing
Real-time stream processing engine.

#### 06-advanced/04-edge.md - Edge-Cloud Collaboration
Edge deployment and cloud synchronization.

#### 06-advanced/05-data-in - Zero-Code Data Connectors
Data ingestion from various sources:

| Subdirectory/File | Description |
|-------------------|-------------|
| index.md | Overview |
| 00-install-agent.md | Agent installation |
| 01-migrate.md | Data migration |
| 02-tmq.md | TMQ integration |
| 03-pi.md | PI System connector |
| 05-opcua/ | OPC UA protocol support (client certificates, Ignition, GE server) |
| 06-opcda.md | OPC DA connector |
| 07-mqtt.md | MQTT connector |
| 08-kafka.md | Kafka connector |
| 09-influxdb.md | InfluxDB migration |
| 10-opentsdb.md | OpenTSDB migration |
| 11-csv.md | CSV import |
| 12-aveva-historian.md | AVEVA Historian connector |
| 13-mysql.md | MySQL connector |
| 14-postgres.md | PostgreSQL connector |
| 15-oracle.md | Oracle connector |
| 16-mssql.md | SQL Server connector |
| 17-mongodb.md | MongoDB connector |
| 19-kinghist.md | KingHist connector |
| 20-pulsar.md | Pulsar connector |
| 21-pulsar-tuya.md | Tuya Pulsar connector |
| 22-pspace.md | PSpace connector |

#### 06-advanced/06-tdgpt - AI Agent for Time-Series
Built-in AI capabilities for forecasting and anomaly detection:

| Subdirectory/File | Description |
|-------------------|-------------|
| index.md | Overview |
| 01-introduction.md | Introduction to TDgpt |
| 02-tutorial.md | Tutorial guide |
| 03-management.md | Management operations |
| 04-preprocess.md | Data preprocessing |
| 05-forecast/ | Forecasting algorithms (ARIMA, HoltWinters, TSFM) |
| 06-anomaly-detection/ | Anomaly detection methods (statistics, data density, ML) |
| 07-correlation.md | Correlation analysis |
| 08-imputation.md | Data imputation |
| 09-dev/ | Development guide for custom algorithms |

#### 06-advanced/07-data-publisher - Data Publishing
| File | Description |
|------|-------------|
| index.md | Overview |
| 01-mqtt.md | MQTT publishing |
| 02-flink.md | Flink integration |

### 07 - Developer's Guide
Application development with TDengine.

| File | Description |
|------|-------------|
| index.md | Overview and development workflow |
| 01-connect.md | Connection methods (REST, native, WebSocket) |
| 02-sql.md | SQL usage in applications |
| 04-schemaless.md | Schemaless write mode |
| 05-stmt.md | PreparedStatement API |
| 07-tmq.md | TMQ consumer API |
| 09-udf.md | User-defined functions |
| 15-high.md | High-throughput write guide |
| connector-security-best-practices.md | Connector security |
| ssl-configuration-guide.md | SSL/TLS configuration |

### 08 - Operations and Maintenance
Cluster planning, deployment, and maintenance.

| File/Subdirectory | Description |
|-------------------|-------------|
| index.md | Overview |
| 01-intro.md | Introduction |
| 02-planning.md | Cluster planning |
| 03-deployment/ | Deployment methods (manual, Docker, Kubernetes) |
| 04-maintenance.md | Maintenance procedures |
| 05-monitor.md | Monitoring guide |
| 09-backup.md | Backup strategies |
| 10-disaster.md | Disaster recovery |
| 12-multi.md | Multi-site deployment |
| 13-network.md | Network configuration |
| 14-user.md | User management |
| 16-security.md | Security features |
| 17-security-suggestions.md | Security recommendations |
| 18-ha/ | High availability (3-replica, 2-replica, dual-active) |
| 19-debug.md | Debugging guide |
| 21-performance/ | Performance tuning |

### 10 - Third-Party Tools
Integration with external tools and platforms.

#### 10-third-party/01-collection - Data Collection
| File | Description |
|------|-------------|
| index.md | Overview |
| 02-prometheus.md | Prometheus integration |
| 03-telegraf.md | Telegraf integration |
| 05-collectd.md | collectd integration |
| 06-statsd.md | StatsD integration |
| 07-icinga2.md | Icinga2 integration |
| 08-tcollector.md | tcollector integration |
| 09-emq-broker.md | EMQ broker integration |
| 11-kafka.md | Kafka integration |
| 12-flink.md | Flink integration |

#### 10-third-party/03-visual - Visualization
| File | Description |
|------|-------------|
| index.md | Overview |
| 01-grafana.md | Grafana integration |
| 02-perspective.md | Perspective integration |

#### 10-third-party/05-bi - Business Intelligence
| File | Description |
|------|-------------|
| index.md | Overview |
| 01-looker.md | Looker integration |
| 03-powerbi.md | Power BI integration |
| 09-seeq.md | Seeq integration |
| 11-superset.md | Apache Superset integration |
| 12-tableau.md | Tableau integration |
| 13-excel.md | Excel integration |
| 15-spark.md | Spark integration |
| 16-ssrs.md | SSRS integration |
| 17-ontop.md | Ontop integration |
| 18-pandas.md | Pandas integration |

#### 10-third-party/07-tool - Database Tools
| File | Description |
|------|-------------|
| index.md | Overview |
| 01-dbeaver.md | DBeaver integration |
| 03-qstudio.md | QStudio integration |

#### 10-third-party/09-iot - IoT Platforms
| File | Description |
|------|-------------|
| index.md | Overview |
| 01-node-red.md | Node-RED integration |
| 02-ignition.md | Ignition integration |

### 14 - Reference
Detailed reference documentation.

| Subdirectory | Description |
|--------------|-------------|
| index.md | Overview |
| 01-components/ | TDengine components (taosd, taosAdapter, taosKeeper, Explorer) |
| 02-tools/ | Built-in tools (taos CLI, taosdump, taosBenchmark) |
| 03-taos-sql/ | Complete SQL reference manual |
| 05-connector/ | Connector reference for various languages (Java, Python, Go, C/C++, etc.) |

### 26 - Inside TDengine
Internal design and architecture documentation.

### 27 - Frequently Asked Questions
Common issues and solutions covering:
- Installation & Deployment
- Connection issues
- Data Writing
- Data Query
- Data Subscription
- Operations & Monitoring
- Upgrade & Migration
- Client & Tools

### 28 - Releases
Version history and download links for TDengine 3.x releases.

### 29 - Roadmap
Product roadmap and planned features for future releases.

---

## Quick Navigation

| Target Audience | Recommended Sections |
|-----------------|----------------------|
| Beginners | [Get Started](04-get-started/index.md) |
| System Architects | [Basic Features](05-basic/index.md), [Advanced Features](06-advanced/index.md) |
| Developers | [Developer's Guide](07-develop/index.md) |
| Administrators | [Operations](08-operation/index.md), [Reference](14-reference/index.md) |

## Documentation Source

This documentation is part of the open-source TDengine project. The source code is available at [GitHub](https://github.com/taosdata/tdengine).

For errors or unclear descriptions, submit corrections via the **Edit this page** link on each documentation page.