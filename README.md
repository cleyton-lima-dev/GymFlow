# 🏋️ GymFlow

> Sistema SaaS para gerenciamento de treinos em academias.

O **GymFlow** é uma plataforma desenvolvida para auxiliar academias no gerenciamento de alunos, professores e treinos, oferecendo uma experiência moderna tanto para o profissional quanto para o aluno.

Este projeto nasceu para atender uma necessidade real de uma academia e está sendo desenvolvido seguindo práticas profissionais de engenharia de software, com foco em escalabilidade, organização e qualidade de código.

---

## 🚀 Objetivo

Desenvolver uma plataforma que permita aos professores criar e gerenciar treinos personalizados, enquanto os alunos acompanham sua rotina de exercícios diretamente pelo aplicativo.

O sistema foi projetado para ser **multitenant**, permitindo que diversas academias utilizem a mesma plataforma com total isolamento dos seus dados.

---

## ✨ Funcionalidades do MVP

### 👨‍🏫 Professor

- Login
- Cadastro de alunos
- Cadastro de exercícios
- Criação de treinos
- Organização dos treinos por dia
- Adição de observações
- Atualização de treinos

### 🧑‍💪 Aluno

- Login
- Visualização dos treinos
- Marcar treino como concluído
- Histórico de treinos
- Visualização dos dados pessoais

---

## 📈 Roadmap

### ✅ Versão 1.0

- Autenticação
- Cadastro de alunos
- Cadastro de exercícios
- Gerenciamento de treinos
- Histórico de treinos

### 🔜 Futuras versões

- Fotos dos exercícios
- Vídeos demonstrativos
- Evolução de carga
- Avaliação física
- Medidas corporais
- Gráficos de evolução
- Notificações
- Chat entre professor e aluno
- Temporizador de descanso
- Check-in via QR Code

---

## 🏗️ Arquitetura

O projeto será dividido em módulos independentes.

```text
GymFlow
│
├── Backend (.NET)
├── Mobile (Flutter)
├── Banco de Dados (PostgreSQL)
└── Documentação
```

A arquitetura seguirá os princípios de:

- Clean Architecture
- SOLID
- Repository Pattern
- Service Layer
- REST API

---

## 🛠️ Tecnologias

### Backend

- ASP.NET Core 10
- C#
- Entity Framework Core
- PostgreSQL
- JWT Authentication
- FluentValidation
- Serilog

### Mobile

- Flutter
- Dart

### Banco de Dados

- PostgreSQL

### Ferramentas

- Git
- GitHub
- Docker
- Swagger / OpenAPI

---

## 📁 Estrutura do Projeto

```text
GymFlow
│
├── .github
├── assets
├── backend
├── database
├── docs
├── mobile
├── scripts
├── LICENSE
└── README.md
```

---

## 📚 Documentação

Toda a documentação técnica será criada durante o desenvolvimento e ficará disponível na pasta `docs`.

---

## 🎯 Status

🚧 Em desenvolvimento

---

## 🤝 Contribuição

Este projeto está sendo desenvolvido inicialmente para uso em uma academia parceira e posteriormente evoluirá para uma plataforma SaaS.

---

## 👨‍💻 Desenvolvedor

**Cleyton Lima**

GitHub:
https://github.com/cleyton-lima-dev

LinkedIn:
(Em breve)

---

## 📄 Licença

Este projeto está licenciado sob a licença MIT.