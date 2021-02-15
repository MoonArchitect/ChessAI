# ChessAI C# (Archived)

First attempt on creating chess AI.  
Move generation is done using ChessLib by rudzen (https://github.com/rudzen/ChessLib)

### Performance (Ryzen 3900x, 1 core) 
 - Board Evaluation ~12M/s 
 - Move Generation  ~500k/s

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

### Evaluation
 - [x] Material Evaluation
 - [x] Piece-Square Tables
 - [x] Pawn Structures
 - Isolated Pawns
 - Passed Pawns
 - Open Pawns
 - Defended Pawns
 - Bakcward Pawns
 - [x] King Safety
 - [x] Rook on 7th Rank
 - [x] Rooks Defence
 - [x] Rook/Bishop/Knight Pair
   
