# ChessAI C# (Archived)

First attempt on creating chess AI.  
Move generation is done using ChessLib by rudzen (https://github.com/rudzen/ChessLib)

### Performance 
 - 1C Board Evaluations ~12M/s 
 - 1C Moves Generation ~500k/s

### Methods
 - Multithreaded:
 - [x] Minimax
 - [x] Negamax
 - [x] Alpha-Beta  
 - [x] Quiescence Search  
 - [x] NegaScout  
 - [x] NegaScout + QSearch
 - [x] zwSearch
 - [x] PVS - Principal Variation Search
 - [x] PVS + zwSearch
 
### Heuristics
 - [x] Aspiration Windows   
 - [x] Static Null move heuristic  
 - [x] Adaptive Null move
 - [x] PV-Move
 - [x] Futility Pruning
 - [x] Transposition Table using Zobrist hashing   
 - [x] MVV-LVA (Most Valuable Victim - Least Valuable Aggressor)
