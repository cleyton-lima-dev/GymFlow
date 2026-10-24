# PIN de acesso físico — limite e cache

O PIN de quatro dígitos é emitido ou redefinido pelo Admin. A API guarda somente
o HMAC associado ao GymId. Tentativas online de PIN reservam capacidade em
`PhysicalAccessPinAttemptStates` antes de consultar a credencial. A linha por
academia é compartilhada por todas as instâncias da API e persiste a reinícios.
No PostgreSQL, a janela e o bloqueio usam o relógio do banco, evitando
divergência entre servidores da API.
Acertos liberam a reserva; cinco falhas em um minuto bloqueiam novas tentativas
por cinco minutos. Cartões e biometria não usam esse limite.

O Agent mantém seu próprio limite offline no SQLite. Uma permissão PIN em cache
só pode autorizar acesso até cinco minutos após `UpdatedAt`, mesmo que tenha
sido gravada por uma versão antiga com `ValidUntil` de 30 minutos. Entrada sem
data de gravação comprovável é negada para PIN; as regras de cache de cartão e
biometria permanecem iguais. Reiniciar o Agent não renova a validade.

Durante desconexão, uma redefinição ou revogação feita na API não chega
imediatamente ao Agent: um PIN anteriormente autorizado pode continuar válido
até o fim dessa janela residual de no máximo cinco minutos. A próxima decisão
online substitui o cache; expirado o prazo, o acesso por PIN falha fechado.
