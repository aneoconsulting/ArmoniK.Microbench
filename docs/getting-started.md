# Getting Started

## Project Structure

The ArmoniK Microbenchmark project is split into 4 main components

<div class="grid cards" markdown>

-   __Microbenchmark CLI__ 
    
    ---
    
    This will be the main tool that you'll be interacting with to run your microbenchmarks. The Microbenchmark CLI allows you to create a study (#TODO: Reference page on that) which is a higher level structure that groups all the elements relevant to running a microbenchmark (information on the resources deployed, tags, as well as the results of said benchmark)   (link to studies)

-   __Infrastructure__
    
    ---
    
    Terraform code for deploying machines for running the microbenchmarks as well as the various components that ArmoniK supports.

-   __Benchmark Runner__
    
    ---
    
    CLI tool that takes in a benchmark config file and then uses BenchmarkDotNet to execute the relevantly defined microbenchmark.

-   { .lg .middle } __Visualization tool__
    
    ---
    
    Streamlit application that takes in a study and then generates an interactive visualization.
</div>


