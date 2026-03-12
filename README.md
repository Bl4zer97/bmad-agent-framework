# Introduction

This repository provides a **centralized set of guidelines, agents and prompts**
to enable and govern the use of **GitHub Copilot and AI-assisted delivery** across projects.

The objective of this repository is **not** to deliver application code, but to:
- standardize how AI tools are used
- ensure consistency, quality and compliance
- provide a reusable baseline for multiple projects

---

# Getting Started

This repository is **not a project template**.

Projects are expected to **consume this repository via Git submodules**, typically by linking:
- agents
- instructions
- reusable prompts

## Installation process
There is no installation process in the traditional sense.
Refer to the project documentation to see how this repository is linked as a submodule.

## Software dependencies
There are no runtime dependencies.
Any required tooling is project-specific and documented at project level.

## Latest releases
This repository is versioned through Git commits and branches.
Projects are responsible for pinning the desired version via submodules.

## API references
Not applicable.
This repository does not expose APIs or executable components.

---

# Build and Test

This repository does not contain buildable or deployable code.

There are:
- no binaries
- no compilation steps
- no automated tests

Its purpose is to **influence and guide AI behavior**, not to be executed.

---

# Contribute

Contributions are welcome.

If you want to improve agents, instructions or prompts:
- follow the existing structure and conventions
- keep changes focused and well documented
- ensure backward compatibility where possible

Please refer to the contribution guidelines in this repository before submitting changes.

If you want to learn more about creating good readme files then refer the following [guidelines](https://docs.microsoft.com/en-us/azure/devops/repos/git/create-a-readme?view=azure-devops). You can also seek inspiration from the below readme files:
- [ASP.NET Core](https://github.com/aspnet/Home)
- [Visual Studio Code](https://github.com/Microsoft/vscode)
- [Chakra Core](https://github.com/Microsoft/ChakraCore)