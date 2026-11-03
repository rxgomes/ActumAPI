-- Prepara um PostgreSQL instalado na máquina para desenvolvimento da Actum API.
-- Executar como superusuário (ex.: postgres):
--   psql -U postgres -f scripts/criar-usuario-dev.sql
-- Credenciais apenas para desenvolvimento.

create role actum with login password 'actum' createdb;

create database actum owner actum;

-- Extensão usada pela busca sem acento (módulo Tarefas). A migration 20261103_Unaccent também a cria; aqui
-- garante a criação caso o usuário actum não tenha permissão para isso.
\connect actum
create extension if not exists unaccent;
