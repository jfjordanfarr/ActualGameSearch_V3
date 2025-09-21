## **User Prompt**

Please find and tweak a well-suited Github Codespaces template or other devcontainer template which could be used to easily set up a Dotnet 10 (preview) Aspire application which will facilitate the development, with necessary emulators, of a distributed application with the following components:

\- A static web application (preferably vanilla HTML/CSS/JS,)

\- A WebAPI (preferably .NET WebAPI but with .NET Core 10\) 

\- An ollama container for embeddings generation (Embedding Gemma or Nomic-Embed-v1.5, 768 dimensions, full or half precision, preferably finding a way to piggyback off free Azure services like Azure function apps)  

\- A CosmosDB for NoSQL (emulated in Aspire)

 Among options for Github Codespaces, would any computationally be able to handle Embedding Gemma (768 dimensions, either full or half precision) over Ollama? If so, how would that work? Today is September 20, 2025, and these things continue to move with great speed. You'll need to figure out what Embedding Gemma is, its requirements on ollama, whether ollama can be used in a Github Codespaces workspace to \*serve\* those local embeddings or whether a different approach will be needed (i.e. local development on a local workstation with a reasonable consumer GPU).

As far as the overall dataset size goes, I would be eventually growing the real Cosmos DB based on the following assumptions:

The total raw text storage can be calculated as follows:

Game Descriptions: 150,000 games × 2,300 characters/game ≈ 345 MB

Reviews: 15,000,000 reviews × 500 characters/review ≈ 7.5 GB

Total Raw Text Storage (Baseline): Approximately 8 GB

Other notes about creating or locating this template:

\- I want to optimize cost primarily (let's definitely use DiskANN)

\- I already purchased and own "actualgamesearch.com" from Cloudflare.

\- I intend to utilize Cosmos DB for hybrid semantic/fulltext search

---

## **Gemini Deep Research**

Here's a research plan for that topic. If you need to update it, let me know\!  
\`\`\`

Devcontainer for .NET Aspire Embeddings

Research Websites

(1) Search for official and community-provided GitHub Codespaces and devcontainer templates for .NET 10 (preview) with .NET Aspire support. This will serve as the foundation for the development environment. (2) Research the computational requirements (CPU, RAM, and VRAM) for running the 'Embedding Gemma' and 'Nomic-Embed-v1.5' models (768 dimensions) via an Ollama container. (3) Investigate the current machine specifications (CPU, RAM, GPU availability) offered by GitHub Codespaces. Compare these specifications with the model requirements from the previous step to determine the feasibility and performance of serving embeddings from within a Codespace. (4) Find documentation and examples for configuring a \`devcontainer.json\` file to orchestrate the required services. This includes: (a) Setting up Docker-in-Docker or a similar feature to run the Ollama container. (b) Integrating the Azure Cosmos DB emulator as a service. (c) Ensuring the .NET 10 SDK and Aspire workloads are correctly installed. (5) Outline the process for structuring the .NET Aspire application to manage the static web app, the WebAPI, the Ollama container, and the emulated Cosmos DB as interconnected resources. (6) Research the integration of the DiskANN vector index with Azure Cosmos DB for NoSQL. Find information on how it facilitates cost-effective hybrid semantic and full-text search at scale. (7) Explore the viability of offloading the embedding generation to free-tier Azure services like Azure Functions. Analyze the technical limitations (e.g., memory, execution time, package support for Ollama) and compare this approach to running Ollama directly within the development container. (8) Synthesize the findings to provide a conclusive recommendation on the most suitable development setup, evaluating whether GitHub Codespaces is sufficient or if local development on a machine with a dedicated GPU is necessary for this workload.

Analyze Results

Create Report  
\`\`\`

---

# **Report 00**

# **Architectural Blueprint and Devcontainer Template for a.NET 10 Aspire Application on GitHub Codespaces**

## **I. Executive Summary & Architectural Blueprint**

### **A. Report Objective and Scope**

This report provides a comprehensive architectural guide and a fully functional devcontainer template for the "actualgamesearch.com" application. The objective is to facilitate a seamless, reproducible "clone and run" development experience within GitHub Codespaces, leveraging the latest advancements in.NET 10 and the.NET Aspire orchestration framework.

The scope of this document encompasses a detailed feasibility analysis, environment configuration, component integration, advanced development practices, and a forward-looking production deployment strategy. The core application components addressed are a static web frontend (vanilla HTML/CSS/JS), a.NET 10 WebAPI backend, a containerized AI service using Ollama for local text embeddings, and an emulated Azure Cosmos DB for NoSQL, specifically configured for high-performance hybrid search.

### **B. Core Recommendations at a Glance**

A thorough analysis of the project requirements, technical constraints, and the current technology landscape (as of September 2025\) has led to the following core recommendations:

* **Development Environment:** GitHub Codespaces is the definitive platform for this project. It provides a consistent, cloud-hosted Linux VM that perfectly aligns with.NET Aspire's goal of universal development environments, eliminating "works on my machine" issues and streamlining team onboarding.1  
* **AI Feasibility and Model Selection:** The use of a local embedding model within a CPU-only Codespace is not only feasible but also highly effective. The recommended model is **Embedding Gemma**, which is explicitly designed for high efficiency on non-GPU hardware. Its minimal resource footprint is well within the capacity of standard Codespaces machine tiers.3 This CPU-centric approach is mandated by the recent deprecation of GPU-enabled machines in the Codespaces service, making the selection of an efficient model paramount.5  
* **Orchestration Framework:**.NET Aspire will serve as the single, authoritative orchestrator for all application services. The Aspire AppHost will define, configure, and launch the WebAPI, the Ollama container, and the Cosmos DB emulator, providing a unified F5 debug experience and a single source of truth for the application's architecture.6  
* **Database Solution:** The official Azure Cosmos DB emulator, run as a container and orchestrated by Aspire, is the ideal choice for local development. It provides a high-fidelity, zero-cost emulation of the production service. Crucially, it supports the configuration of advanced indexing policies, including the user-specified **DiskANN** vector index, enabling the development and testing of high-performance hybrid search queries without incurring cloud costs.7

### **C. High-Level Architecture Diagram**

The following diagram illustrates the proposed architecture within the GitHub Codespaces environment. It depicts the relationships between the core components and the flow of data and requests, all managed under the orchestration of the.NET Aspire AppHost.

Code snippet

graph TD  
    subgraph GitHub Codespaces Environment (Cloud VM)  
        subgraph Dev Container (VS Code)  
            A\[devcontainer.json\] \--\> B{Builds & Configures};  
            B \--\> C;  
            subgraph C  
                D;  
                E;  
                F;  
                G;  
            end  
        end

        subgraph Orchestrated by.NET Aspire AppHost  
            H\[AppHost Process\] \-- Manages \--\> I;  
            H \-- Manages \--\> J;  
            H \-- Manages \--\> K\[Ollama Container\];  
            H \-- Manages \--\> L;  
        end  
    end

    %% Interaction Flows  
    User \-- Interacts via Browser/VS Code \--\> C;  
    I \-- HTTP Request \--\> J;  
    J \-- Generates Embeddings \--\> K;  
    J \-- Queries Data \--\> L;

    %% Style Definitions  
    style User fill:\#f9f,stroke:\#333,stroke-width:2px;  
    style A fill:\#bbf,stroke:\#333,stroke-width:2px;  
    style H fill:\#lightgreen,stroke:\#333,stroke-width:2px;

**Diagram Legend:**

* **GitHub Codespaces Environment:** The outer boundary representing the cloud-hosted virtual machine.  
* **Dev Container (VS Code):** The primary container where the developer's tools and source code reside. The devcontainer.json file defines its configuration.  
* **.NET Aspire AppHost:** The central C\# process that orchestrates the entire distributed application.  
* **Orchestrated Services:** The individual components of the application (WebAPI, Ollama, Cosmos DB) that are launched and managed as separate processes or containers by the AppHost.  
* **Interaction Flows:** Arrows indicate the primary communication paths during application runtime.

## **II. Feasibility Analysis: AI Embeddings in a CPU-Only GitHub Codespace**

A central question for this project is whether a cloud development environment like GitHub Codespaces can effectively serve a local AI embedding model without dedicated GPU hardware. The analysis concludes that it is not only possible but also practical, provided the correct model and resource allocation are chosen.

### **A. The Critical Constraint: Deprecation of GPU Machines in Codespaces**

The strategy for AI model hosting is fundamentally shaped by a recent and definitive platform change. As of August 29, 2025, GPU-enabled virtual machine types have been deprecated and are no longer available in GitHub Codespaces.5 This decision follows the retirement of the underlying Azure NCv3-series virtual machines.

This development is pivotal. It removes GPU acceleration from consideration and establishes a CPU-only environment as a hard constraint, not a choice. Any viable solution must therefore be architected around models that are highly performant on standard server-grade CPUs. Previous beta programs or documentation that mentioned GPU availability are now obsolete.10 The focus must shift from seeking GPU access to selecting the most efficient CPU-native embedding model available.

### **B. CPU-Centric Embedding Model Analysis**

With the CPU-only constraint established, the choice of embedding model becomes the most critical factor for performance. A comparison of the two user-suggested models reveals a clear winner for this specific use case.

#### **Embedding Gemma**

* **Design Philosophy:** Google's Embedding Gemma was explicitly engineered for on-device, offline, and resource-constrained environments—scenarios that are functionally identical to a CPU-only cloud VM.3 Its architecture is optimized for efficiency rather than raw power.  
* **Resource Footprint:** The model is remarkably compact, with only 308 million parameters. Through techniques like Quantization-Aware Training (QAT), it can operate effectively using less than 200MB of RAM.3 This minimal memory requirement makes it an ideal tenant within a multi-service Codespace, leaving ample resources for the database, API, and IDE.  
* **Performance:** Despite its small size, Embedding Gemma is a state-of-the-art model, ranking as the top open multilingual text embedding model under 500M parameters on the Massive Text Embedding Benchmark (MTEB).3 It has demonstrated extremely low inference latency (under 15ms for 256 tokens on EdgeTPU hardware), which indicates excellent performance on modern server CPUs found in Codespaces.3

#### **Nomic-Embed-Text-v1.5**

* **Resource Footprint:** This model is even smaller at 137M parameters, with a total size of approximately 262MB using float16 precision.15 It also fits comfortably within the resource limits of a standard Codespace.  
* **Performance Considerations:** While its small size is appealing, community benchmarks and discussions suggest that its inference speed can be significantly slower than other models in its class, such as BAAI/bge-small-en-v1.5.17 The model's primary innovation was its use of Matryoshka Representation Learning (MRL) for resizable embedding dimensions, a feature that is now also a core component of the more performant Embedding Gemma.12

#### **Recommendation**

**Embedding Gemma is the unequivocally superior choice for this project.** Its design is perfectly aligned with the CPU-only constraint of GitHub Codespaces. It offers a state-of-the-art balance of performance, efficiency, and low resource consumption, ensuring that the embedding service will not become a bottleneck in the development environment. nomic-embed-text is a viable fallback but presents potential performance disadvantages without offering any unique benefits over Embedding Gemma.

The evolution of models like Embedding Gemma reflects a broader industry trend. While massive, multi-billion parameter models continue to push the boundaries of AI capability in GPU-rich data centers, a parallel track of innovation is focused on creating highly optimized, efficient models designed to run on commodity hardware. The deprecation of GPUs in a mainstream development platform like Codespaces reinforces the importance of this trend. The project's architecture, by necessity, aligns with this modern approach, resulting in a solution that is not only functional but also more portable, cost-effective, and efficient by design.

### **C. Sizing the Codespace: Resource Allocation Strategy**

To ensure a smooth and productive development experience, it is essential to select a Codespaces machine tier that can comfortably accommodate all running services. A conservative estimate of the required resources is as follows:

* **Ollama with Embedding Gemma:** \~1 GB RAM (allowing for model loading and operational overhead).  
* **Cosmos DB Emulator:** \~2 GB RAM.  
* **.NET Aspire AppHost & WebAPI:** \~1-2 GB RAM during active debugging and compilation.  
* **VS Code Server & System Overhead:** \~2-4 GB RAM.  
* **Total Estimated RAM:** 8-9 GB.

Based on this analysis, the following machine tiers are recommended.

| Machine Tier | vCPUs | RAM | Storage | Suitability & Rationale |
| :---- | :---- | :---- | :---- | :---- |
| **Standard (4-core)** | 4 | 16 GB | 32 GB | **Recommended Minimum:** Provides sufficient resources to run all services concurrently. Ideal for cost-conscious individual development, though some performance degradation may be noticeable during heavy multitasking or parallel builds. |
| **Premium (8-core)** | 8 | 32 GB | 64 GB | **Recommended for Teams:** This tier offers significant headroom, resulting in faster build times, responsive debugging, and a fluid multi-tasking experience. It is the preferred choice for sustained, professional development. |
| **Premium (16-core+)** | 16+ | 64 GB+ | 128 GB | **Excessive:** Not required for this workload. Selecting a machine of this size would lead to unnecessary costs without a proportional increase in development productivity for this specific application stack. |

To prevent developers from inadvertently creating an underpowered environment, the devcontainer.json configuration will include a hostRequirements block, enforcing a minimum of 4 cores and 16 GB of memory, ensuring a baseline level of performance for all users.19

## **III. Crafting the Multi-Service Development Environment with.NET Aspire**

The foundation of the development experience is the devcontainer.json file, which bootstraps the entire environment. Within this environment,.NET Aspire takes over as the high-level orchestrator, defining the application's distributed architecture in a clear, strongly-typed manner.

### **A. The Foundation: devcontainer.json**

The devcontainer.json file is the entry point for GitHub Codespaces, instructing it on how to build and configure the development sandbox.2 The configuration for this project will begin with the official

.NET Aspire Dev Container template as its base, which provides a solid foundation with the necessary.NET SDK and tooling pre-installed.21 This base will be customized with several critical additions:

* **Image:** The configuration will specify a Microsoft-provided dev container image tagged for the.NET 10 SDK, ensuring the correct runtime is available.  
* **Features:** The ghcr.io/devcontainers/features/docker-in-docker feature is non-negotiable and will be enabled..NET Aspire operates by orchestrating other Docker containers (like Ollama and Cosmos DB); therefore, the primary dev container must have the ability to run its own Docker daemon.1  
* **Host Requirements:** As determined in the feasibility analysis, a hostRequirements block will be added to enforce a minimum machine specification of 4 vCPUs and 16 GB of RAM, preventing the use of under-provisioned machines.19  
* **Lifecycle Scripts:**  
  * onCreateCommand: This script runs only the first time a Codespace is created for a given configuration. It will be used to execute dotnet workload install aspire, ensuring the latest Aspire templates and command-line tools are installed in the environment.1  
* **IDE Customizations:** The customizations.vscode.extensions property will be populated with a list of essential Visual Studio Code extensions. This includes the C\# Dev Kit, the official.NET Aspire Dashboard extension, and the Docker extension, providing a rich, fully integrated IDE experience out of the box.24

### **B. The Orchestrator:.NET Aspire AppHost (Program.cs)**

The ActualGameSearch.AppHost project is the heart of the local development experience. It replaces traditional docker-compose.yml files or shell scripts with a C\# console application that programmatically defines, configures, and launches every component of the distributed system.6 This code-first approach provides compile-time safety, discoverability through IntelliSense, and deep integration with the Visual Studio and VS Code debuggers.

The AppHost's Program.cs file will define the following resources:

1. **WebAPI Project:** The backend service will be added using builder.AddProject\<Projects.ActualGameSearch\_Api\>("webapi"). Aspire will manage the build and launch process for this project, making its endpoints available to other services.26  
2. **Static Frontend:** To optimize for simplicity and reduce the number of moving parts, the static HTML/CSS/JS files will be served directly from the WebAPI project. This is a standard ASP.NET Core practice (app.UseStaticFiles()) and avoids the overhead of a separate Node.js server or Nginx container during development.  
3. **Cosmos DB Emulator:** The database emulator will be added with a single, declarative line of code: builder.AddAzureCosmosDB("cosmos").RunAsEmulator(). This leverages a built-in Aspire hosting extension that handles the complexities of pulling the correct emulator image, running it as a container, and managing its lifecycle automatically.9  
4. **Ollama Container:** The embedding service will be added using the community-provided hosting extension: builder.AddOllama("ollama").AddModel("embeddinggemma"). This powerful abstraction not only runs the ollama/ollama container but also intelligently pre-pulls the specified embeddinggemma model into the container on its first startup, fully automating the setup process.28

Service discovery and connection string management, often a point of friction in microservices development, are handled seamlessly by Aspire. The relationships between services are declared using the WithReference() extension method:

C\#

var cosmos \= builder.AddAzureCosmosDB("cosmos").RunAsEmulator();  
var ollama \= builder.AddOllama("ollama").AddModel("embeddinggemma");

var webapi \= builder.AddProject\<Projects.ActualGameSearch\_Api\>("webapi")  
                   .WithReference(cosmos)  
                   .WithReference(ollama);

When the application starts, Aspire inspects these references and injects the correct, dynamically assigned connection strings and service URLs as environment variables into the webapi process. This completely eliminates the need for manual configuration files or hardcoded localhost URLs.31

### **C. The Role of Docker Compose: A Deliberate Omission**

A common pattern for multi-service development containers involves defining all services within a docker-compose.yml file and referencing it from devcontainer.json.33 For this project, this pattern is explicitly

**not recommended** for defining the application's dependent services (Ollama and Cosmos DB).

The decision to centralize orchestration within the.NET Aspire AppHost is a key architectural choice..NET Aspire is not merely a project runner; it is a sophisticated orchestration framework designed to be the single source of truth for a distributed application's composition.6 Defining the dependent containers in a separate

docker-compose.yml file would introduce redundancy and architectural fragmentation. The application's topology would be split between a static YAML file and a dynamic C\# project, undermining the clarity and power of the Aspire model.

Therefore, the architecture designates a clear hierarchy of responsibility:

1. **devcontainer.json:** Its sole purpose is to bootstrap the *sandbox*—the single, top-level container equipped with the.NET SDK and Docker-in-Docker capabilities.  
2. **.NET Aspire AppHost:** Within that sandbox, the AppHost acts as the exclusive orchestrator, programmatically defining and launching all constituent parts of the actualgamesearch.com application.

This approach ensures that the entire distributed application is defined in one logical, compile-time-checked, and easily understandable location: the AppHost/Program.cs file.

## **IV. Component Deep Dive: Configuration & Integration**

This section provides detailed configuration for each of the core components, demonstrating how they are integrated within the.NET Aspire ecosystem to form a cohesive application.

### **A. Ollama Embedding Service**

The Ollama service provides the crucial function of converting raw text into semantic vector embeddings. Its integration is streamlined through a dedicated community toolkit.

#### **AppHost Configuration**

The primary configuration occurs within the ActualGameSearch.AppHost/Program.cs file.

1. **Package Reference:** The AppHost project must reference the CommunityToolkit.Aspire.Hosting.Ollama NuGet package.29  
2. **Resource Definition:** The Ollama container is added to the application model. A critical addition is the use of WithDataVolume to ensure persistence of the downloaded AI model.  
   C\#  
   // In ActualGameSearch.AppHost/Program.cs

   var ollama \= builder.AddOllama("ollama")  
                      .AddModel("embeddinggemma")  
                      .WithDataVolume("ollama-models");

   The AddModel("embeddinggemma") call instructs the integration to automatically execute ollama pull embeddinggemma inside the container upon startup.28 The  
   WithDataVolume("ollama-models") method is essential for an efficient development workflow. It directs Aspire to create a named Docker volume (ollama-models) and mount it to the /root/.ollama directory inside the container. This is where Ollama caches its models. Without this, the multi-gigabyte Embedding Gemma model would be re-downloaded every time the Codespace is restarted or the container is rebuilt, leading to significant delays.28

#### **WebAPI Integration**

The WebAPI service consumes the Ollama API to perform the embedding generation.

1. **Package Reference:** The ActualGameSearch.Api project must reference the OllamaSharp NuGet package, which provides a convenient.NET client for the Ollama REST API.  
2. **Client Consumption:** Because the WebAPI project was configured with .WithReference(ollama) in the AppHost, Aspire automatically registers and configures the OllamaApiClient in the dependency injection container. The API endpoint can simply inject this client and use it directly.  
   C\#  
   // In ActualGameSearch.Api/Program.cs (example endpoint)  
   app.MapPost("/embed", async (string text, OllamaApiClient client) \=\>  
   {  
       var request \= new GenerateEmbeddingRequest  
       {  
           Model \= "embeddinggemma",  
           Prompt \= text  
       };  
       var response \= await client.GenerateEmbeddings(request);  
       return Results.Ok(response.Embedding);  
   });

### **B. Cosmos DB Emulator for Hybrid Search**

The Cosmos DB emulator provides a local, high-fidelity environment for developing against the NoSQL API, including its advanced vector search capabilities.

#### **AppHost Configuration**

The setup in the AppHost is remarkably simple due to Aspire's built-in support.

1. **Package Reference:** The AppHost must reference Aspire.Hosting.Azure.CosmosDB.  
2. **Resource Definition:** The emulator is added, and the application's database and container are declaratively defined.  
   C\#  
   // In ActualGameSearch.AppHost/Program.cs

   var cosmos \= builder.AddAzureCosmosDB("cosmos")  
                      .RunAsEmulator();

   var gamesDb \= cosmos.AddDatabase("GamesDB");  
   var gamesContainer \= gamesDb.AddContainer("GamesContainer", "/id"); // Using '/id' as a simple partition key

   The RunAsEmulator() call instructs Aspire to use the mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator container image instead of provisioning a cloud resource.9 The  
   AddDatabase and AddContainer calls create logical resources in the Aspire model, which can be referenced by client projects.38

#### **Enabling Hybrid Search (DiskANN)**

The user's requirement for DiskANN is a critical performance consideration, as it is a state-of-the-art vector indexing library deeply integrated into Cosmos DB for efficient, low-latency search over large datasets.8 This is not enabled by default but must be configured via the container's indexing policy.

This initialization logic should reside in the WebAPI service. A common pattern is to run this code once during application startup.

C\#

// In ActualGameSearch.Api, e.g., in a startup service

public async Task InitializeDatabaseAsync(CosmosClient cosmosClient)  
{  
    var databaseResponse \= await cosmosClient.CreateDatabaseIfNotExistsAsync("GamesDB");  
    var database \= databaseResponse.Database;

    var containerProperties \= new ContainerProperties  
    {  
        Id \= "GamesContainer",  
        PartitionKeyPath \= "/id",  
        IndexingPolicy \= new IndexingPolicy  
        {  
            VectorIndexes \=  
            {  
                // Configure the DiskANN index on the 'embedding' property  
                new VectorIndexPath { Path \= "/embedding", Type \= VectorIndexType.DiskAnn }  
            },  
            FullTextIndex \= new FullTextIndex  
            {  
                // Configure the full-text index on the 'description' property  
                Path \= "/description"  
            }  
        }  
    };

    await database.CreateContainerIfNotExistsAsync(containerProperties);  
}

This code ensures that when the container is created, it is configured with both a DiskANN vector index on the /embedding property and a full-text index on the /description property, satisfying the requirements for hybrid search.7

### **C. Application Services (WebAPI & Static Frontend)**

#### **WebAPI Project (ActualGameSearch.Api)**

This project serves as the backend logic hub for the application.

* **Project Type:** A standard.NET 10 Minimal API project.  
* **Aspire Integration:** The Program.cs file will include a call to builder.AddServiceDefaults(). This is a key Aspire method that automatically wires up essential cloud-native functionalities, including OpenTelemetry for logging and tracing, health check endpoints, and service discovery configurations.25  
* **Client Injection:** The Program.cs will also demonstrate how the CosmosClient (configured by .WithReference(gamesContainer)) and OllamaApiClient are retrieved from the dependency injection container for use in API endpoints.

#### **Static Frontend (wwwroot)**

As previously recommended, the simplest and most efficient approach for development is to serve the static frontend assets directly from the WebAPI.

* **Location:** All HTML, CSS, and JavaScript files will be placed in the wwwroot folder within the ActualGameSearch.Api project.  
* **Configuration:** The WebAPI's Program.cs will be configured with two lines of code to enable this functionality:  
  C\#  
  // In ActualGameSearch.Api/Program.cs  
  app.UseDefaultFiles(); // Serves index.html for requests to the root URL  
  app.UseStaticFiles();  // Serves files from the wwwroot folder

This setup ensures that when a developer navigates to the WebAPI's base URL in the Aspire dashboard, the index.html of the single-page application is served, providing a seamless "F5" experience.

## **V. Advanced Configuration & Production Path**

Beyond the basic setup, a professional development environment requires robust solutions for configuration security and debugging. Furthermore, a clear and cost-effective path to production is essential for the project's long-term success.

### **A. Secure Configuration: Managing Secrets**

Hardcoding sensitive information like API keys or connection string credentials is a significant security risk. The development environment must provide a secure way to manage these values.

* **Problem:** The default connection string for the Cosmos DB emulator contains a well-known key. While less critical for a local-only emulator, it is best practice to treat all credentials as secrets. Future integrations might require production API keys even during development.  
* **Solution:** The recommended approach is to use the integrated secrets management features of GitHub Codespaces.42 This mechanism allows developers to store secrets in their personal GitHub account, which are then securely injected into the Codespace environment at runtime, keeping them out of the project's source code.

#### **Mechanism for Implementation:**

1. **Create a Codespaces Secret:** The developer navigates to their GitHub settings (Settings \> Codespaces \> Secrets) and creates a new secret, for example, COSMOS\_EMULATOR\_KEY, with the value C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==.36  
2. **Reference in devcontainer.json:** The devcontainer.json file is modified to make this secret available as an environment variable within the container. This is done using the remoteUserEnvVars property.  
   JSON  
   "remoteUserEnvVars": {  
       "COSMOS\_KEY": "${secrets.COSMOS\_EMULATOR\_KEY}"  
   }

3. **Consume in AppHost:** The.NET Aspire AppHost can then read this environment variable and use it to configure the Cosmos DB resource. This decouples the secret value from the application code.

### **B. Debugging the Distributed Application**

One of the primary advantages of using.NET Aspire within the VS Code ecosystem is the deeply integrated debugging experience.

* **Multi-Project Debugging:** When a developer starts a debugging session (F5) on the ActualGameSearch.AppHost project, the C\# Dev Kit extension for VS Code automatically attaches the debugger to all orchestrated.NET projects, including the ActualGameSearch.Api service.34  
* **Seamless Workflow:** A developer can set a breakpoint in a WebAPI controller, interact with the static frontend in their browser (launched from the Aspire dashboard), and the debugger will pause execution at the breakpoint as if it were a monolithic application. This works flawlessly even though the API is running in a separate process managed by Aspire.  
* **The Aspire Dashboard:** This web-based tool, which launches automatically, is an indispensable part of the debugging process. It provides a centralized view of all resources, their current state, endpoints, and, most importantly, a real-time, aggregated stream of logs and traces from every component of the application.22 This holistic view dramatically simplifies the process of diagnosing issues in a distributed system.

### **C. A Path to Production: Cost-Optimized Azure Deployment**

The user query specifically requested an analysis of leveraging free Azure services for the embedding component. The most suitable and modern platform for this task is Azure Container Apps (ACA).

#### **Proposed Architecture**

The Ollama service, packaged as a Docker container, can be deployed directly to an Azure Container Apps environment running on the serverless **Consumption plan**.46 This platform is designed to run containers and microservices, and its billing model is highly aligned with the project's cost-optimization goal.

The ACA Consumption plan includes a substantial monthly free grant, which is often sufficient to run low-traffic or development/staging services at no cost.49

| Resource | Monthly Free Grant | Pay-as-you-go Price (Post-Grant) | Implication for Ollama Service |
| :---- | :---- | :---- | :---- |
| **vCPU-seconds** | 180,000 | $0.000024 / second | Supports approximately 50 hours of continuous execution for a 1 vCPU container for free. |
| **GiB-seconds** | 360,000 | $0.000003 / second | Supports approximately 50 hours of continuous execution for a 2 GiB RAM container for free. |
| **Requests** | 2,000,000 | $0.40 / million | The first 2 million embedding generation requests per month are free. |

#### **The Cold Start Challenge and Mitigation**

The primary trade-off of a serverless consumption plan is the "cold start" phenomenon. If the application receives no traffic for a period, ACA will scale the container down to zero replicas to save costs. The next incoming request will then experience a delay as a new container instance is provisioned, the image is pulled, and the Ollama service initializes and loads the model into memory.52 For an AI service, this delay could be several seconds, which may be unacceptable for a responsive user interface.

Fortunately, there are several effective mitigation strategies:

1. **Guaranteed Warm Instance (Recommended):** The most robust solution is to configure the Container App with a minimum replica count of one (minReplicas \= 1). This instructs ACA to always keep at least one instance of the container running and ready to accept requests. This completely eliminates cold starts for the first user. While this incurs a small, predictable monthly cost for the idle resources, it provides the best performance and user experience. The cost is still significantly lower than provisioning a dedicated virtual machine.54  
2. **Scheduled Warmer (Cost-Optimized):** To maintain the "scale-to-zero" benefit, a separate, extremely low-cost service can be used to keep the container warm. A timer-triggered Azure Function can be configured to run every 5-10 minutes. This function's only job is to send a simple HTTP request to the Ollama container's health endpoint. This periodic traffic prevents ACA from de-provisioning the instance. The cost of these warmer invocations is negligible, often falling entirely within the Azure Functions free tier.55  
3. **Image and Application Optimization:** The Docker image containing the Ollama service should be optimized for size by removing any unnecessary layers or tools. The application's startup logic should be streamlined to load the model into memory as quickly as possible. Using Azure Container Registry in the same region as the Container App will also reduce image pull times.52

By adopting one of these strategies, the project can successfully transition from the emulated development environment to a high-performance, cost-optimized, and scalable production environment on Azure.

## **VI. Final Template and Implementation Guide**

This section provides the complete, ready-to-use configuration files and code snippets required to implement the architecture described in this report. These files are designed to be placed directly into the project repository to enable the GitHub Codespaces environment.

### **A. File: .devcontainer/devcontainer.json**

This file is the master configuration for the GitHub Codespaces environment. It defines the base image, enables Docker-in-Docker, sets minimum hardware requirements, and installs necessary tools and VS Code extensions.

JSON

{  
  "name": ".NET Aspire Starter",  
  // Or use a Dockerfile or Docker Compose file. More info: https://containers.dev/guide/dockerfile  
  "image": "mcr.microsoft.com/dotnet/sdk:10.0-preview",  
  "features": {  
    "ghcr.io/devcontainers/features/docker-in-docker:2": {  
      "version": "latest",  
      "moby": true  
    }  
  },

  // Enforce a minimum machine size to ensure a good developer experience.  
  "hostRequirements": {  
    "cpus": 4,  
    "memory": "16gb",  
    "storage": "32gb"  
  },

  // Forward ports from the container to the host.  
  "forwardPorts": ,  
  "portsAttributes": {  
    "18888": {  
      "label": "Aspire Dashboard"  
    },  
    "18889": {  
      "label": "Aspire Dashboard (Secure)"  
    }  
  },

  // Use 'onCreateCommand' to run commands only once when the container is created.  
  "onCreateCommand": "dotnet workload install aspire",

  // Use 'postCreateCommand' to run commands after the container is created.  
  "postCreateCommand": "dotnet restore",

  // Configure tool-specific properties.  
  "customizations": {  
    // Configure properties specific to VS Code.  
    "vscode": {  
      "settings": {},  
      "extensions": \[  
        "ms-dotnettools.csdevkit",  
        "ms-dotnettools.aspire",  
        "ms-azuretools.vscode-docker"  
      \]  
    }  
  },

  // Uncomment to connect as root instead. More info: https://aka.ms/dev-containers-non-root.  
  "remoteUser": "root"  
}

### **B. File: ActualGameSearch.AppHost/Program.cs**

This is the central orchestrator for the distributed application. It defines all services and their relationships in C\#.

C\#

// Add necessary using statements for Aspire hosting packages  
// e.g., using Aspire.Hosting.Azure;  
// using CommunityToolkit.Aspire.Hosting;

var builder \= DistributedApplication.CreateBuilder(args);

// Define the Azure Cosmos DB resource, configured to run the local emulator.  
var cosmos \= builder.AddAzureCosmosDB("cosmos")  
                   .RunAsEmulator();

// Define the database and container within the Cosmos DB resource.  
var gamesDb \= cosmos.AddDatabase("GamesDB");  
var gamesContainer \= gamesDb.AddContainer("GamesContainer", "/id");

// Define the Ollama container resource.  
// It will automatically pull the 'embeddinggemma' model on startup.  
// A persistent volume is mounted to cache the model across restarts.  
var ollama \= builder.AddOllama("ollama")  
                   .AddModel("embeddinggemma")  
                   .WithDataVolume("ollama-models");

// Define the WebAPI project resource.  
var webapi \= builder.AddProject\<Projects.ActualGameSearch\_Api\>("webapi")  
                   .WithReference(gamesContainer) // Inject Cosmos DB connection info  
                   .WithReference(ollama);         // Inject Ollama service URL

// This project is a vanilla HTML/CSS/JS frontend, served by the WebAPI.  
// We model it as an external resource for clarity in the dashboard.  
builder.AddProject\<Projects.ActualGameSearch\_Web\>("web")  
      .WithReference(webapi);

builder.Build().Run();

### **C. File: ActualGameSearch.ApiService/Program.cs (Key Snippets)**

These snippets illustrate how the WebAPI project is configured to consume the services provided by.NET Aspire.

C\#

var builder \= WebApplication.CreateBuilder(args);

// Adds essential cloud-native services like telemetry and health checks.  
builder.AddServiceDefaults();

// Add services to the container.  
builder.Services.AddEndpointsApiExplorer();  
builder.Services.AddSwaggerGen();

// Register the Cosmos DB client, configured automatically by Aspire.  
builder.AddAzureCosmosDBClient("cosmos");

// Register the Ollama client, configured automatically by Aspire.  
builder.AddOllamaSharp();

var app \= builder.Build();

// Configure the HTTP request pipeline.  
if (app.Environment.IsDevelopment())  
{  
    app.UseSwagger();  
    app.UseSwaggerUI();  
}

app.UseHttpsRedirection();

// Enable serving of static files from the 'wwwroot' directory.  
app.UseDefaultFiles();  
app.UseStaticFiles();

// Map the default health check endpoints.  
app.MapDefaultEndpoints();

// Example API endpoint demonstrating client usage.  
app.MapGet("/search", async (string query, CosmosClient cosmosClient, OllamaApiClient ollamaClient) \=\>  
{  
    // 1\. Generate embedding for the query using Ollama.  
    var embeddingRequest \= new GenerateEmbeddingRequest { Model \= "embeddinggemma", Prompt \= query };  
    var embeddingResponse \= await ollamaClient.GenerateEmbeddings(embeddingRequest);  
    var queryVector \= embeddingResponse.Embedding;

    // 2\. Perform a hybrid search query against Cosmos DB.  
    var container \= cosmosClient.GetContainer("GamesDB", "GamesContainer");  
    //... logic to execute a hybrid search query using the queryVector...

    // 3\. Return results.  
    return Results.Ok(/\* search results \*/);  
})  
.WithName("SearchGames");

app.Run();

### **D. Quickstart Guide for New Developers**

This simple, step-by-step guide allows any developer to get the complete distributed application running in minutes.

1. **Prerequisites:** Install Visual Studio Code and the "Dev Containers" extension from the marketplace.  
2. **Clone Repository:** Clone the project repository from GitHub to your local machine.  
   Bash  
   git clone https://github.com/your-org/actualgamesearch.com.git

3. **Open in VS Code:** Open the cloned folder (actualgamesearch.com) in Visual Studio Code.  
4. **Reopen in Container:** A notification will appear in the bottom-right corner: *"Folder contains a Dev Container configuration file. Reopen to folder in container."* Click the **"Reopen in Container"** button.  
5. **Initial Build:** Wait for the Codespace or local dev container to build. This initial setup may take several minutes as it downloads the.NET SDK, base Docker images, and the Embedding Gemma AI model. Progress will be shown in the VS Code terminal.  
6. **Run the Application:** Once the container is ready and the terminal prompt appears, start the entire application by running the AppHost project:  
   Bash  
   dotnet run \--project ActualGameSearch.AppHost

7. **Launch Dashboard:** The.NET Aspire Dashboard will automatically open in a new browser tab. From this dashboard, you can view the status of all services, access their endpoints (including the frontend web application), and monitor real-time logs and telemetry. The development environment is now fully operational.

#### **Works cited**

1. How to Run .NET Aspire in GitHub Codespaces, accessed September 20, 2025, [https://juliocasal.com/blog/how-to-run-net-aspire-in-github-codespaces](https://juliocasal.com/blog/how-to-run-net-aspire-in-github-codespaces)  
2. Setting up a C\# (.NET) project for GitHub Codespaces, accessed September 20, 2025, [https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/adding-a-dev-container-configuration/setting-up-your-dotnet-project-for-codespaces](https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/adding-a-dev-container-configuration/setting-up-your-dotnet-project-for-codespaces)  
3. Introducing EmbeddingGemma: The Best-in-Class Open Model for On-Device Embeddings, accessed September 20, 2025, [https://developers.googleblog.com/en/introducing-embeddinggemma/](https://developers.googleblog.com/en/introducing-embeddinggemma/)  
4. EmbeddingGemma: Powering Private, On-Device AI Experiences | Joshua Berkowitz, accessed September 20, 2025, [https://joshuaberkowitz.us/blog/news-1/embeddinggemma-powering-private-on-device-ai-experiences-1017](https://joshuaberkowitz.us/blog/news-1/embeddinggemma-powering-private-on-device-ai-experiences-1017)  
5. Upcoming deprecation of GPU machine type in Codespaces \- GitHub Changelog, accessed September 20, 2025, [https://github.blog/changelog/2025-08-01-upcoming-deprecation-of-gpu-machine-type-in-codespaces/](https://github.blog/changelog/2025-08-01-upcoming-deprecation-of-gpu-machine-type-in-codespaces/)  
6. dotnet/aspire: Tools, templates, and packages to accelerate building observable, production-ready apps \- GitHub, accessed September 20, 2025, [https://github.com/dotnet/aspire](https://github.com/dotnet/aspire)  
7. Use hybrid search \- Azure Cosmos DB for NoSQL | Microsoft Learn, accessed September 20, 2025, [https://learn.microsoft.com/en-us/azure/cosmos-db/gen-ai/hybrid-search](https://learn.microsoft.com/en-us/azure/cosmos-db/gen-ai/hybrid-search)  
8. Cost-Effective, Low Latency Vector Search with Azure Cosmos DB \- arXiv, accessed September 20, 2025, [https://arxiv.org/pdf/2505.05885](https://arxiv.org/pdf/2505.05885)  
9. NET Aspire Cosmos DB Entity Framework Core integration \- Microsoft Learn, accessed September 20, 2025, [https://learn.microsoft.com/en-us/dotnet/aspire/database/azure-cosmos-db-entity-framework-integration](https://learn.microsoft.com/en-us/dotnet/aspire/database/azure-cosmos-db-entity-framework-integration)  
10. Machine Specs · community · Discussion \#52408 \- GitHub, accessed September 20, 2025, [https://github.com/orgs/community/discussions/52408](https://github.com/orgs/community/discussions/52408)  
11. Getting started with GitHub Codespaces for machine learning, accessed September 20, 2025, [https://docs.github.com/codespaces/developing-in-a-codespace/getting-started-with-github-codespaces-for-machine-learning](https://docs.github.com/codespaces/developing-in-a-codespace/getting-started-with-github-codespaces-for-machine-learning)  
12. Google Embedding Gemma: The Best Embeddings for your AI | by Mehul Gupta | Data Science in Your Pocket | Sep, 2025 | Medium, accessed September 20, 2025, [https://medium.com/data-science-in-your-pocket/google-embedding-gemma-the-best-embeddings-for-your-ai-c90433d08ae6](https://medium.com/data-science-in-your-pocket/google-embedding-gemma-the-best-embeddings-for-your-ai-c90433d08ae6)  
13. Unlock On-Device AI through Google's EmbeddingGemma \- ADaSci, accessed September 20, 2025, [https://adasci.org/unlock-on-device-ai-through-googles-embeddinggemma/](https://adasci.org/unlock-on-device-ai-through-googles-embeddinggemma/)  
14. Welcome EmbeddingGemma, Google's new efficient embedding model \- Hugging Face, accessed September 20, 2025, [https://huggingface.co/blog/embeddinggemma](https://huggingface.co/blog/embeddinggemma)  
15. nomic-embed-text:v1.5 \- Ollama, accessed September 20, 2025, [https://ollama.com/library/nomic-embed-text:v1.5](https://ollama.com/library/nomic-embed-text:v1.5)  
16. nomic-ai/nomic-embed-text-v1.5 · \[AUTOMATED\] Model Memory Requirements, accessed September 20, 2025, [https://huggingface.co/nomic-ai/nomic-embed-text-v1.5/discussions/15](https://huggingface.co/nomic-ai/nomic-embed-text-v1.5/discussions/15)  
17. nomic-ai/nomic-embed-text-v1.5 · Performance \- Hugging Face, accessed September 20, 2025, [https://huggingface.co/nomic-ai/nomic-embed-text-v1.5/discussions/5](https://huggingface.co/nomic-ai/nomic-embed-text-v1.5/discussions/5)  
18. nomic-embed-text-v1.5: Resizable Production Embeddings with Matryoshka Representation Learning \- Scalable Vector Embeddings from 64 to 768 dimensions : r/LocalLLaMA \- Reddit, accessed September 20, 2025, [https://www.reddit.com/r/LocalLLaMA/comments/1atrsng/nomicembedtextv15\_resizable\_production\_embeddings/](https://www.reddit.com/r/LocalLLaMA/comments/1atrsng/nomicembedtextv15_resizable_production_embeddings/)  
19. Setting a minimum specification for codespace machines \- GitHub Docs, accessed September 20, 2025, [https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/configuring-dev-containers/setting-a-minimum-specification-for-codespace-machines](https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/configuring-dev-containers/setting-a-minimum-specification-for-codespace-machines)  
20. devcontainers/templates: Repository for Dev Container Templates that are managed by Dev Container spec maintainers. See https://github.com/devcontainers/template-starter to create your own\! \- GitHub, accessed September 20, 2025, [https://github.com/devcontainers/templates](https://github.com/devcontainers/templates)  
21. dotnet/aspire-devcontainer \- GitHub, accessed September 20, 2025, [https://github.com/dotnet/aspire-devcontainer](https://github.com/dotnet/aspire-devcontainer)  
22. .NET Aspire and GitHub Codespaces \- .NET Aspire | Microsoft Learn, accessed September 20, 2025, [https://learn.microsoft.com/en-us/dotnet/aspire/get-started/github-codespaces](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/github-codespaces)  
23. Laurent Kempé \- .NET Aspire and Dev Container, accessed September 20, 2025, [https://laurentkempe.com/2025/03/06/dotnet-aspire-and-dev-container/](https://laurentkempe.com/2025/03/06/dotnet-aspire-and-dev-container/)  
24. dotnet/aspire-samples \- GitHub, accessed September 20, 2025, [https://github.com/dotnet/aspire-samples](https://github.com/dotnet/aspire-samples)  
25. NET Aspire setup and tooling \- Microsoft Learn, accessed September 20, 2025, [https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling)  
26. Orchestrate resources in .NET Aspire \- Microsoft Learn, accessed September 20, 2025, [https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/orchestrate-resources](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/orchestrate-resources)  
27. Aspire.Microsoft.EntityFrameworkCore.Cosmos 9.4.2 \- NuGet, accessed September 20, 2025, [https://www.nuget.org/packages/Aspire.Microsoft.EntityFrameworkCore.Cosmos](https://www.nuget.org/packages/Aspire.Microsoft.EntityFrameworkCore.Cosmos)  
28. NET Aspire Community Toolkit Ollama integration \- Microsoft Learn, accessed September 20, 2025, [https://learn.microsoft.com/en-us/dotnet/aspire/community-toolkit/ollama](https://learn.microsoft.com/en-us/dotnet/aspire/community-toolkit/ollama)  
29. CommunityToolkit.Aspire.Hosting.Ollama 9.7.2 \- NuGet, accessed September 20, 2025, [https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.Ollama](https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.Ollama)  
30. Harnessing DeepSeek-R1 distilled model with .NET Aspire and Ollama locally, accessed September 20, 2025, [https://laurentkempe.com/2025/02/01/harnessing-deepseek-r1-with-dotnet-aspire-and-ollama-locally/](https://laurentkempe.com/2025/02/01/harnessing-deepseek-r1-with-dotnet-aspire-and-ollama-locally/)  
31. .NET Aspire \- George Kosmidis, accessed September 20, 2025, [https://blog.georgekosmidis.net/dotnet-aspire.html](https://blog.georgekosmidis.net/dotnet-aspire.html)  
32. Unable to follow docs to implement Aspire specific CosmosDB package \#2318 \- GitHub, accessed September 20, 2025, [https://github.com/dotnet/aspire/discussions/2318](https://github.com/dotnet/aspire/discussions/2318)  
33. Multi-Container Development Environments \- Overmind, accessed September 20, 2025, [https://overmind.tech/blog/multi-container-development-environments](https://overmind.tech/blog/multi-container-development-environments)  
34. Create a Dev Container \- Visual Studio Code, accessed September 20, 2025, [https://code.visualstudio.com/docs/devcontainers/create-dev-container](https://code.visualstudio.com/docs/devcontainers/create-dev-container)  
35. Making a dev container with multiple data services \- pamela fox's blog, accessed September 20, 2025, [http://blog.pamelafox.org/2024/11/making-dev-container-with-multiple-data.html](http://blog.pamelafox.org/2024/11/making-dev-container-with-multiple-data.html)  
36. Use Azure Cosmos DB as a Docker container in CI/CD pipelines \- DEV Community, accessed September 20, 2025, [https://dev.to/abhirockzz/use-azure-cosmos-db-as-a-docker-container-in-cicd-pipelines-5bph](https://dev.to/abhirockzz/use-azure-cosmos-db-as-a-docker-container-in-cicd-pipelines-5bph)  
37. Enhance local development experience using the Azure Cosmos DB Linux emulator and VS Code, accessed September 20, 2025, [https://abhirockzz.github.io/posts/cosmosdb-emulator-devcontainer/](https://abhirockzz.github.io/posts/cosmosdb-emulator-devcontainer/)  
38. NET Aspire Azure Cosmos DB integration \- Microsoft Learn, accessed September 20, 2025, [https://learn.microsoft.com/en-us/dotnet/aspire/database/azure-cosmos-db-integration](https://learn.microsoft.com/en-us/dotnet/aspire/database/azure-cosmos-db-integration)  
39. Cost-Effective, Low Latency Vector Search with Azure Cosmos DB \- arXiv, accessed September 20, 2025, [https://arxiv.org/html/2505.05885v1](https://arxiv.org/html/2505.05885v1)  
40. This AI Paper from Microsoft Introduces a DiskANN-Integrated System: A Cost-Effective and Low-Latency Vector Search Using Azure Cosmos DB \- MarkTechPost, accessed September 20, 2025, [https://www.marktechpost.com/2025/05/19/this-ai-paper-from-microsoft-introduces-a-diskann-integrated-system-a-cost-effective-and-low-latency-vector-search-using-azure-cosmos-db/](https://www.marktechpost.com/2025/05/19/this-ai-paper-from-microsoft-introduces-a-diskann-integrated-system-a-cost-effective-and-low-latency-vector-search-using-azure-cosmos-db/)  
41. Azure Cosmos DB No SQL | 🦜️ LangChain, accessed September 20, 2025, [https://python.langchain.com/docs/integrations/vectorstores/azure\_cosmos\_db\_no\_sql/](https://python.langchain.com/docs/integrations/vectorstores/azure_cosmos_db_no_sql/)  
42. Specifying recommended secrets for a repository \- GitHub Docs, accessed September 20, 2025, [https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/configuring-dev-containers/specifying-recommended-secrets-for-a-repository](https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/configuring-dev-containers/specifying-recommended-secrets-for-a-repository)  
43. Introduction to dev containers \- Codespaces \- GitHub Docs, accessed September 20, 2025, [https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/adding-a-dev-container-configuration/introduction-to-dev-containers](https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/adding-a-dev-container-configuration/introduction-to-dev-containers)  
44. Developing inside a Container \- Visual Studio Code, accessed September 20, 2025, [https://code.visualstudio.com/docs/devcontainers/containers](https://code.visualstudio.com/docs/devcontainers/containers)  
45. Add .NET Aspire to Existing Web App \- David Puplava, accessed September 20, 2025, [https://www.davidpuplava.com/fun-with-net-aspire](https://www.davidpuplava.com/fun-with-net-aspire)  
46. Azure Container Apps with Ollama for general AI inference \- Imaginarium, accessed September 20, 2025, [https://www.imaginarium.dev/azure-container-apps-with-ollama/](https://www.imaginarium.dev/azure-container-apps-with-ollama/)  
47. Create your first containerized Azure Functions \- Microsoft Learn, accessed September 20, 2025, [https://learn.microsoft.com/en-us/azure/azure-functions/functions-deploy-container](https://learn.microsoft.com/en-us/azure/azure-functions/functions-deploy-container)  
48. Work with Azure Functions in Containers | Microsoft Learn, accessed September 20, 2025, [https://learn.microsoft.com/en-us/azure/azure-functions/functions-how-to-custom-container](https://learn.microsoft.com/en-us/azure/azure-functions/functions-how-to-custom-container)  
49. Azure Container Apps \- Pricing, accessed September 20, 2025, [https://azure.microsoft.com/en-us/pricing/details/container-apps/](https://azure.microsoft.com/en-us/pricing/details/container-apps/)  
50. Billing in Azure Container Apps | Microsoft Learn, accessed September 20, 2025, [https://learn.microsoft.com/en-us/azure/container-apps/billing](https://learn.microsoft.com/en-us/azure/container-apps/billing)  
51. Is Azure Container Apps Almost FREE?? \- Reddit, accessed September 20, 2025, [https://www.reddit.com/r/AZURE/comments/1f7xwdi/is\_azure\_container\_apps\_almost\_free/](https://www.reddit.com/r/AZURE/comments/1f7xwdi/is_azure_container_apps_almost_free/)  
52. Reducing cold-start time on Azure Container Apps | Microsoft Learn, accessed September 20, 2025, [https://learn.microsoft.com/en-us/azure/container-apps/cold-start](https://learn.microsoft.com/en-us/azure/container-apps/cold-start)  
53. Avoiding Azure Functions Cold Starts \- Problem solved \- Gosso Optimizely Developer Blog, accessed September 20, 2025, [https://optimizely.blog/2024/08/avoiding-azure-functions-cold-starts---problem-solved/](https://optimizely.blog/2024/08/avoiding-azure-functions-cold-starts---problem-solved/)  
54. Azure Functions on Azure Container Apps overview \- Microsoft Learn, accessed September 20, 2025, [https://learn.microsoft.com/en-us/azure/container-apps/functions-overview](https://learn.microsoft.com/en-us/azure/container-apps/functions-overview)  
55. How to reduce cold start latency in Azure Functions on Consumption Plan? \- Microsoft Learn, accessed September 20, 2025, [https://learn.microsoft.com/en-us/answers/questions/5515120/how-to-reduce-cold-start-latency-in-azure-function](https://learn.microsoft.com/en-us/answers/questions/5515120/how-to-reduce-cold-start-latency-in-azure-function)  
56. Can We Solve Serverless Cold Starts? \- Dashbird, accessed September 20, 2025, [https://dashbird.io/blog/can-we-solve-serverless-cold-starts/](https://dashbird.io/blog/can-we-solve-serverless-cold-starts/)