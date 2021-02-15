using System;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;


using Rudz.Chess.Factories;
using Rudz.Chess;
using Rudz.Chess.Enums;
using Rudz.Chess.Types;
using Rudz.Chess.Transposition;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Rudz.Chess.Hash;

namespace testGame
{
    class Program
    {
        const ulong GlobalSeed = 128691275UL;

        static void Main(string[] args)
        {
            var str = "rn1qkbnr/pp2pppp/2p5/3pPb2/3P4/8/PPP2PPP/RNBQKBNR w KQkq - 0 22";
            //var str = "5r2/8/1p2k1n1/6p1/1p2P2p/1P2KP1P/P1P3P1/5R2 w - - 1 41"; // c2c4 - Wrong, depth>=10, En Pas capture by black
            int depth = 7;
            int nThreads = 1;

            var move = new MoveEvaluator(str, depth, "Original", GlobalSeed);
            //Console.WriteLine(move.EvalSpeed(20000));
            //Console.WriteLine(move.MoveGenSpeed(20000));
            //Console.ReadKey();

            while (true)
            {
                Console.WriteLine("Fen:");
                if (str.Length == 0)
                    str = Console.ReadLine();

                move = new MoveEvaluator(str, depth, "Original", GlobalSeed);

                // MakeMove
                // + Commands [params]

                EvaluateGameAsync(str, depth, nThreads, true);

                move.ResetAlphaBeta();

                Console.WriteLine(move.getEvals() + "\t" + move.getFullGenCalls());

                Console.WriteLine("Depth - ");
                depth = int.Parse(Console.ReadLine());

                str = "";
            }
        }

        static MoveEvaluator[] EvaluateGameAsync(string board, int depth, int nTreads, bool printInfo) {
            int evaluatedMovesCnt = 0;

            var move = new MoveEvaluator(board, depth, "Original", GlobalSeed);
            var white = move.getSide() == 1 ? false : true;

            int score = white ? int.MinValue : int.MaxValue;

            var moveList = move.GetAllMoves();
            var nMoves = moveList.GetLength(0);
            var results = new int[nMoves];
            if (depth > 9)
            {
                var shallowSearch = EvaluateGameAsync(board, depth - 4, nTreads, false);
                shallowSearch[0].ResetAlphaBeta();
                for (int j = 0; j < nMoves; j++)
                {
                    results[j] = shallowSearch[j].reachedScore;
                }
                // Sort
                for (int i = 0; i < nMoves; i++)
                {
                    for (int j = 0; j < nMoves; j++)
                    {
                        if (results[i] > results[j]) {
                            var tempScore = results[i]; results[i] = results[j]; results[j] = tempScore;
                            var tempStr = moveList[i, 0]; moveList[i, 0] = moveList[j, 0]; moveList[j, 0] = tempStr;
                            var tempMove = moveList[i, 1]; moveList[i, 1] = moveList[j, 1]; moveList[j, 1] = tempMove;
                        }
                    }
                }
            }

            var moveEvaluation = new MoveEvaluator[nMoves];

            for (int i = 0; i < nMoves; i++)
            {
                moveEvaluation[i] = new MoveEvaluator(moveList[i, 0], depth - 1, moveList[i, 1], GlobalSeed);
            }
            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            for (int i = 0; i < Math.Min(nMoves, nTreads); i++)
            {
                moveEvaluation[i].Evaluate(); evaluatedMovesCnt++;
            }

            bool AllExited = false;
            var ConsoleUpdate = Stopwatch.StartNew();

            var sw = Stopwatch.StartNew();

            while (!AllExited)
            {
                AllExited = true;

                for (int i = 0; i < nMoves; i++)
                {
                    var thread = moveEvaluation[i];

                    if (thread.status == ThreadStatus.Pending || thread.status == ThreadStatus.Evaluating)
                    {
                        AllExited = false;
                        continue;
                    }

                    if (!thread.CheckedByHost)
                    {
                        if (evaluatedMovesCnt < nMoves)
                        {
                            moveEvaluation[evaluatedMovesCnt].Evaluate(); evaluatedMovesCnt++;
                        }

                        if (white)
                        {
                            score = Math.Max(score, thread.reachedScore);
                        }
                        else {
                            score = Math.Min(score, thread.reachedScore);
                        }

                        if (printInfo && ConsoleUpdate.ElapsedMilliseconds > 750) {
                            Console.Clear();
                            Console.WriteLine(String.Format("{0, -7} {1, -7} {2, -15} {3, 8} {4, 20} {5, 20} {6, 20} {7, 20} {8, 20}", "Score", "Move", "Type", "Eval", "Gens", "Eval/s", "Gen/s", "TT hits", "re-search"));
                            moveEvaluation
                                .OrderByDescending(m => m.reachedScore)
                                .ToList()
                                .ForEach(m => Console.WriteLine(m));

                            ConsoleUpdate.Restart();
                        }

                        thread.CheckedByHost = true;
                    }

                }

                Thread.Sleep(20);
            }

            sw.Stop();
            ConsoleUpdate.Stop();

            //if (printInfo)
            //{
            Console.Clear();
            Console.WriteLine(String.Format("{0, -7} {1, -7} {2, -15} {3, 8} {4, 20} {5, 20} {6, 20} {7, 20} {8, 20}", "Score", "Move", "Type", "Eval", "Gens", "Eval/s", "Gen/s", "TT hits", "re-search"));
            moveEvaluation
                .OrderByDescending(m => m.reachedScore)
                .ToList()
                .ForEach(m => Console.WriteLine(m));

            Console.WriteLine("Time Ellapsed - " + sw.ElapsedMilliseconds);
            Console.WriteLine("%" + getPercentage(moveEvaluation) + "\tEvals - " + getAllEvalCalls(moveEvaluation) + "  \tGens - " + getAllGenCalls(moveEvaluation) + "  \tEval/s = " + (int)(getAllEvalCalls(moveEvaluation) * 1000.0 / sw.ElapsedMilliseconds) + "  \tcalls/s = " + (int)(getAllGenCalls(moveEvaluation) * 1000.0 / sw.ElapsedMilliseconds)); //  
            //}
            //else { 
            //    
            //}

            return moveEvaluation;
        }

        static int getAllEvalCalls(MoveEvaluator[] moves)
        {
            int total = 0;

            foreach (var move in moves)
            {
                total += move.getEvals();
            }

            return total;
        }

        static int getAllGenCalls(MoveEvaluator[] moves)
        {
            int total = 0;

            foreach (var move in moves)
            {
                total += move.getFullGenCalls();
            }

            return total;
        }

        static double getPercentage(MoveEvaluator[] moves)
        {
            double total = 0;

            foreach (var move in moves)
            {
                total += ((double)move.betaCutFirstMove / (double)move.betaCut);
            }

            return total / moves.Length;
        }
    }

    class MoveEvaluator
    {
        const bool endGame = false;

        #region varibales

        Dictionary<ulong, TTStructure> TranspositionTable = new Dictionary<ulong, TTStructure>();
        Dictionary<ulong, int> PawnEvaluationTable = new Dictionary<ulong, int>();

        static int Alpha = int.MinValue + 10;
        static int Beta = int.MaxValue - 10;
        static object lockAlpha = new object();
        static object lockBeta = new object();

        int evaluations = 0, fullGenCalls = 0, quietGenCalls = 0, TThits = 0, numberOfReserches = 0;
        public int reachedScore = int.MinValue + 10;
        public NodeType scoreType;
        long msEllapsed = 0;

        IGame board;

        int MoveSide;
        string InitialMove = "";
        int depthToSearch = 0, reachedAlpha = int.MinValue + 10, reachedBeta = int.MaxValue - 10;
        Thread AsyncSearch;

        public ThreadStatus status;
        public bool CheckedByHost = false;

        #endregion

        int QSearchLimit = 12;

        #region Get Debug Info

        public override string ToString()
        {
            double dT = msEllapsed / 1000.0;
            return String.Format("{0, -7} {1, -7} {2, -15} {3, 8} {4, 20} {5, 20} {6, 20} {7, 20} {8, 20}", reachedScore, InitialMove, scoreType, evaluations, fullGenCalls, (int)(evaluations / dT), (int)(fullGenCalls / dT), TThits, numberOfReserches);
        }

        public int getSide() => board.State.SideToMove.Side;

        public int getEvals() => evaluations;

        public int getFullGenCalls() => fullGenCalls;

        public int getQuietGenCalls() => quietGenCalls;

        public long getTimeExecution() => msEllapsed;

        #endregion Get Debug Info

        #region Interface

        public MoveEvaluator(string fenBoard, int depth, string startingMove, ulong _seed)
        {
            depthToSearch = depth;

            IniZobristMatrix(_seed);

            board = GameFactory.Create(fenBoard);
            
            AsyncSearch = new Thread(EvaluateTree);
            status = ThreadStatus.Pending;

            MoveSide = board.State.SideToMove.Side;

            InitialMove = startingMove;
        }

        public void ResetAlphaBeta() {
            Alpha = int.MinValue + 10;
            Beta = int.MaxValue - 10;
        }

        public int betaCutFirstMove = 0, betaCut = 0;

        void EvaluateTree()
        {
            reachedAlpha = Alpha;
            reachedBeta = Beta;

            var timer = Stopwatch.StartNew();

            Node gotRes = new Node { };

            //depthToSearch = 9;
            //QSearchLimit = 35;

            
            //gotRes = -ST_AB(depthToSearch, -reachedBeta, -reachedAlpha);
            //gotRes = -ST_ABS(depthToSearch, -reachedBeta, -reachedAlpha);
            //gotRes = -ST_NS(depthToSearch, -reachedBeta, -reachedAlpha);
            gotRes = -ST_NSS(depthToSearch, -reachedBeta, -reachedAlpha, true, true);

            //gotRes = -NodeNegaScout(depthToSearch, -reachedBeta, -reachedAlpha, false);
            //gotRes = -NodeNegaScout_QSearch(depthToSearch + QSearchLimit, -reachedBeta, -reachedAlpha, true);
            //Console.WriteLine("B1 - " + betaCutFirstMove + "\t1Move - " + firstMove + "\t" + ((double)betaCutFirstMove / firstMove) + "%");

            reachedScore = gotRes.score;
            scoreType = gotRes.type;

            timer.Stop();
            msEllapsed = timer.ElapsedMilliseconds;

            lock (lockAlpha)
            {
                Alpha = Math.Max(Alpha, reachedScore);
            }
            
            //TranspositionTable.Clear();
            //TranspositionTable = null;
            //MoveGenTT.Clear();
            //MoveGenTT = null;
            //GC.Collect();

            status = ThreadStatus.Finished;
        }

        public void Evaluate()
        {
            AsyncSearch.Start();
            status = ThreadStatus.Evaluating;
        }

        public double EvalSpeed(int nTime)
        {
            var sw = Stopwatch.StartNew();
            ulong nCycles = 0;

            string[] lib = new string[] {
                "r2qk2r/pp2bppp/2b1pn2/2pp4/5P2/1P2PN2/PBPP2PP/RN1Q1RK1 w kq -",
                "r3kb1r/pp1q1ppp/2nppn2/8/3pP3/2P2N2/PP3PPP/RNBQR1K1 w kq -",
                "r3kb1r/pp1qpppp/3p4/2pPn3/4n3/2P2N2/PP3PPP/RNBQ1RK1 w kq -",
                "r2qkb1r/pp3ppp/2bppn2/6B1/3QP3/2N2N2/PPP2PPP/R3K2R w KQkq -",
                "rnb1k2r/1pq1bppp/p2ppn2/6B1/3NPP2/2N2Q2/PPP3PP/R3KB1R w KQkq -",
                "rn1qk2r/1p2bppp/p2pbn2/4p3/4P3/1NN1BP2/PPP3PP/R2QKB1R w KQkq -",
                "r1bq1rk1/pp2ppbp/2np1np1/8/3NP3/2N1BP2/PPPQ2PP/R3KB1R w KQ -",
                "r1bqkb1r/5ppp/p1np1n2/1p2p1B1/4P3/N1N5/PPP2PPP/R2QKB1R w KQkq b6",
                "r2qkb1r/1p1b1ppp/p1nppn2/6B1/3NP3/2N5/PPPQ1PPP/2KR1B1R w kq -",
                "r1bqkb1r/1p3pp1/p1nppn1p/6B1/3NP3/2N5/PPPQ1PPP/2KR1B1R w kq -",
                "r1bqkb1r/pp3pp1/2n1pn1p/3p4/3NP1PP/2N5/PPP2P2/R1BQKBR1 w Qkq -",
                "r1bqkb1r/pp3pp1/2nppn2/7p/3NP1PP/2N5/PPP2P2/R1BQKBR1 w Qkq -",
                "r1bqk2r/1p2bppp/p1nppn2/8/2BNP3/2N1B3/PPP1QPPP/R3K2R w KQkq -",
                "r1b1k2r/pp2bppp/1qnppn2/8/2B1P3/1NN5/PPP2PPP/R1BQ1RK1 w kq -",
                "r1bq1rk1/pp2bppp/2nppn2/8/3NP3/2N1B3/PPP1BPPP/R2Q1RK1 w - -",
                "r1bqkb1r/3n1ppp/p2ppn2/1p6/3NP3/2N1BP2/PPPQ2PP/R3KB1R w KQkq -",
                "rnb1k1nr/1pqp1ppp/p3p3/8/1b1NP3/2N3P1/PPP2P1P/R1BQKB1R w KQkq -",
                "r1b1k2r/1pqp1ppp/p1n1pn2/8/1b1NP3/2N1B3/PPPQ1PPP/2KR1B1R w kq -",
                "r1bqkb1r/pppp1pp1/5np1/3P4/8/2PB4/PP3PPP/RNBQK2R w KQkq -",
                "rnbqk2r/ppppbppp/8/3P4/8/2PN4/PP3PPP/RNBQK2R w KQkq -",
                "r1bqkbnr/ppp3pp/2n2p2/1B1pp3/Q3P3/2P2N2/PP1P1PPP/RNB1K2R b KQkq -",
                "r2qkb1r/pppb1ppp/5n2/3P4/2B1p3/2P2Q2/PP1P1PPP/RNB1K2R w KQkq -",
                "2kr1bnr/pppb1ppp/2nq4/4p3/Q1B5/2P2N2/PP1P1PPP/RNB2RK1 w - -",
                "r1b1kbnr/ppp2ppp/2n5/1B1qp3/Q7/2P2N2/PP1P1PPP/RNB1K2R b KQkq -",
                "r1bq1rk1/pppp1ppp/2n5/3nP3/3P4/5N2/PP1Q1PPP/RN2KB1R w KQ -",
                "r2qk2r/ppp1bppp/2npb3/1B1nP3/3P4/2N2N2/PP3PPP/R1BQK2R w KQkq -",
                "r1b1kb1r/ppp3pp/2n2q2/3p4/3pn3/2P2N2/PP1NQPPP/R1B1KB1R w KQkq -",
            };
            int cnt = 0;

            while (sw.ElapsedMilliseconds < nTime)
            {
                board.SetFen(lib[cnt % lib.Length]); cnt++;
                for (int i = 0; i < 100000; i++)
                {
                    StaticEvaluation(board);
                }
                nCycles += 100000;
            }
            sw.Stop();

            return nCycles * 1000.0 / sw.ElapsedMilliseconds;
        }

        public double MoveGenSpeed(int nTime, int depth = 5)
        {
            ulong nCycles = 0;
            string OriginalFen = board.GetFen().ToString();
            string[] lib = new string[] {
                "r2qk2r/pp2bppp/2b1pn2/2pp4/5P2/1P2PN2/PBPP2PP/RN1Q1RK1 w kq -",
                "r3kb1r/pp1q1ppp/2nppn2/8/3pP3/2P2N2/PP3PPP/RNBQR1K1 w kq -",
                "r3kb1r/pp1qpppp/3p4/2pPn3/4n3/2P2N2/PP3PPP/RNBQ1RK1 w kq -",
                "r2qkb1r/pp3ppp/2bppn2/6B1/3QP3/2N2N2/PPP2PPP/R3K2R w KQkq -",
                "rnb1k2r/1pq1bppp/p2ppn2/6B1/3NPP2/2N2Q2/PPP3PP/R3KB1R w KQkq -",
                "rn1qk2r/1p2bppp/p2pbn2/4p3/4P3/1NN1BP2/PPP3PP/R2QKB1R w KQkq -",
                "r1bq1rk1/pp2ppbp/2np1np1/8/3NP3/2N1BP2/PPPQ2PP/R3KB1R w KQ -",
                "r1bqkb1r/5ppp/p1np1n2/1p2p1B1/4P3/N1N5/PPP2PPP/R2QKB1R w KQkq b6",
                "r2qkb1r/1p1b1ppp/p1nppn2/6B1/3NP3/2N5/PPPQ1PPP/2KR1B1R w kq -",
                "r1bqkb1r/1p3pp1/p1nppn1p/6B1/3NP3/2N5/PPPQ1PPP/2KR1B1R w kq -",
                "r1bqkb1r/pp3pp1/2n1pn1p/3p4/3NP1PP/2N5/PPP2P2/R1BQKBR1 w Qkq -",
                "r1bqkb1r/pp3pp1/2nppn2/7p/3NP1PP/2N5/PPP2P2/R1BQKBR1 w Qkq -",
                "r1bqk2r/1p2bppp/p1nppn2/8/2BNP3/2N1B3/PPP1QPPP/R3K2R w KQkq -",
                "r1b1k2r/pp2bppp/1qnppn2/8/2B1P3/1NN5/PPP2PPP/R1BQ1RK1 w kq -",
                "r1bq1rk1/pp2bppp/2nppn2/8/3NP3/2N1B3/PPP1BPPP/R2Q1RK1 w - -",
                "r1bqkb1r/3n1ppp/p2ppn2/1p6/3NP3/2N1BP2/PPPQ2PP/R3KB1R w KQkq -",
                "rnb1k1nr/1pqp1ppp/p3p3/8/1b1NP3/2N3P1/PPP2P1P/R1BQKB1R w KQkq -",
                "r1b1k2r/1pqp1ppp/p1n1pn2/8/1b1NP3/2N1B3/PPPQ1PPP/2KR1B1R w kq -",
                "r1bqkb1r/pppp1pp1/5np1/3P4/8/2PB4/PP3PPP/RNBQK2R w KQkq -",
                "rnbqk2r/ppppbppp/8/3P4/8/2PN4/PP3PPP/RNBQK2R w KQkq -",
                "r1bqkbnr/ppp3pp/2n2p2/1B1pp3/Q3P3/2P2N2/PP1P1PPP/RNB1K2R b KQkq -",
                "r2qkb1r/pppb1ppp/5n2/3P4/2B1p3/2P2Q2/PP1P1PPP/RNB1K2R w KQkq -",
                "2kr1bnr/pppb1ppp/2nq4/4p3/Q1B5/2P2N2/PP1P1PPP/RNB2RK1 w - -",
                "r1b1kbnr/ppp2ppp/2n5/1B1qp3/Q7/2P2N2/PP1P1PPP/RNB1K2R b KQkq -",
                "r1bq1rk1/pppp1ppp/2n5/3nP3/3P4/5N2/PP1Q1PPP/RN2KB1R w KQ -",
                "r2qk2r/ppp1bppp/2npb3/1B1nP3/3P4/2N2N2/PP3PPP/R1BQK2R w KQkq -",
                "r1b1kb1r/ppp3pp/2n2q2/3p4/3pn3/2P2N2/PP1NQPPP/R1B1KB1R w KQkq -",
            };
            int cnt = 0;

            var sw = Stopwatch.StartNew();


            while (sw.ElapsedMilliseconds < nTime)
            {
                board.SetFen(lib[cnt % lib.Length]); cnt++;

                for (int i = 0; i < 10000; i++)
                    genMoves();
                nCycles += 10000;
                //treeGen(depth);
            }

            sw.Stop();

            board.SetFen(OriginalFen);

            return nCycles * 1000.0 / sw.ElapsedMilliseconds;

            void treeGen(int remDepth) {
                if (remDepth > -1)
                {
                    var t = genMoves(); nCycles++;
                    foreach (var tt in t)
                    {
                        board.MakeMove(tt);
                        treeGen(remDepth - 1);
                        board.TakeMove();
                    }
                }
            }
        }

        #endregion

        #region Search Alghoritms

        int[] pieceValues = new int[] { 0, 100, 320, 340, 500, 975, 10000 };

        Node ST_ABS(int depth, int alpha, int beta)
        {
            if (depth == 0)
                return ST_QSearch(depth - 1, alpha, beta);//new Node { score = StaticEvaluation(board) };

            var moves = genCaptureMoves();
            var moveScores = moves.scores;
            var moveCount = moves.Count;
            bool noneCaptures = false;

            if (moveCount == 0)
                noneCaptures = true;

            for (int i = 0; i < moveCount; i++)
            {
                board.MakeMove(moves[getNextMove()]);

                var score = -ST_ABS(depth - 1, -beta, -alpha);

                board.TakeMove();

                if (score.score >= beta)
                {
                    betaCut++;

                    if (i == 0)
                        betaCutFirstMove++;

                    return new Node { score = beta, type = NodeType.CutNode };
                }

                if (score.score > alpha)
                    alpha = score.score;
            }

            moves = genQuietMoves();
            moveScores = moves.scores;
            moveCount = moves.Count;

            if (moveCount == 0 && noneCaptures)
                    return new Node { score = -32000 + depth };

            for (int i = 0; i < moveCount; i++)
            {
                board.MakeMove(moves[getNextMove()]);

                var score = -ST_ABS(depth - 1, -beta, -alpha);

                board.TakeMove();

                if (score.score >= beta)
                {
                    betaCut++;

                    if (i == 0)
                        betaCutFirstMove++;

                    return new Node { score = beta, type = NodeType.CutNode };
                }


                if (score.score > alpha)
                    alpha = score.score;
            }

            return new Node { score = alpha };

            int getNextMove()
            {
                int bestMove = 0;

                for (int i = 1; i < moveCount; i++)
                {
                    if (moveScores[i] > moveScores[bestMove])
                        bestMove = i;
                }

                moveScores[bestMove] = -99999;

                return bestMove;
            }
        }

        Node ST_AB(int depth, int alpha, int beta)
        {

            if (depth == 0)
                return new Node { score = StaticEvaluation(board) };

            var moves = genMoves();
            var moveScores = moves.scores;
            var moveCount = moves.Count;

            if (moveCount == 0)
                return new Node { score = -32000 + depth };

            for (int i = 0; i < moveCount; i++)
            {

                board.MakeMove(moves[i]);

                var score = -ST_AB(depth - 1, -beta, -alpha);

                board.TakeMove();

                if (score.score >= beta)
                {
                    betaCut++;

                    if (i == 0)
                        betaCutFirstMove++;

                    return new Node { score = beta, type = NodeType.CutNode };
                }


                if (score.score > alpha)
                    alpha = score.score;
            }

            return new Node { score = alpha };

            int getNextMove()
            {
                int bestMove = 0;

                for (int i = 1; i < moveCount; i++)
                {
                    if (moveScores[i] > moveScores[bestMove])
                        bestMove = i;
                }

                moveScores[bestMove] = -99999;

                return bestMove;
            }
        }

        Node ST_NS(int depth, int alpha, int beta)
        {
            if (depth == 0)
                return new Node { score = StaticEvaluation(board) };

            var moves = genMoves();
            var moveScores = moves.scores;
            var moveCount = moves.Count;
            bool PVmove = true;

            if (moveCount == 0)
                return new Node { score = -32000 + depth };

            for (int i = 0; i < moveCount; i++)
            {
                Node score;
                var moveId = i;// getNextMove();

                if (PVmove)
                {
                    board.MakeMove(moves[moveId]);
                    score = -ST_NS(depth - 1, -beta, -alpha);
                    board.TakeMove();
                }
                else
                {
                    board.MakeMove(moves[moveId]);
                    score = -ST_NS(depth - 1, -alpha - 1, -alpha);
                    board.TakeMove();

                    if (score.score > alpha && score.score < beta)
                    {
                        numberOfReserches++;
                        board.MakeMove(moves[moveId]);
                        score = -ST_NS(depth - 1, -beta, -alpha);
                        board.TakeMove();
                    }
                }


                if (score.score >= beta)
                {
                    betaCut++;

                    if (i == 0)
                        betaCutFirstMove++;

                    return new Node { score = beta, type = NodeType.CutNode };
                }

                PVmove = false;

                if (score.score > alpha)
                    alpha = score.score;
            }

            return new Node { score = alpha };

            int getNextMove()
            {
                int bestMove = 0;

                for (int i = 1; i < moveCount; i++)
                {
                    if (moveScores[i] > moveScores[bestMove])
                        bestMove = i;
                }

                moveScores[bestMove] = -99999;

                return bestMove;
            }
        }

        Node ST_NSS(int depth, int alpha, int beta, bool PV, bool DO_NULL)
        {
            if (TranspositionTable.ContainsKey(board.State.Key)) //.reachedDepth >= depth
            {
                TTStructure entry = TranspositionTable[board.State.Key];

                if (entry.reachedDepth >= depth)
                {
                    TThits++;

                    if (entry.reachedNode.type == NodeType.Valid)
                        return entry.reachedNode;
                    if (entry.reachedNode.type == NodeType.LBound && alpha < entry.reachedNode.score)
                        alpha = entry.reachedNode.score;
                    if (entry.reachedNode.type == NodeType.UBound && beta > entry.reachedNode.score)
                        beta = entry.reachedNode.score;
                    if (alpha >= beta)
                        return new Node { score = alpha, type = NodeType.CutNode }; // beta - fail hard | alpha - fail soft
                }
            }

            var inCheck = board.Position.InCheck;

            if (depth <= 0 && (!inCheck || depth < -10))
                return ST_QSearch(depth - 1, alpha, beta, true); //new Node { score = StaticEvaluation(board) };//

            var boardKey = board.State.Key;
            var OAlpha = alpha;

            var newDepth = depth - 1;

            var moves = genCaptureMoves();
            var moveScores = moves.scores;
            var moveCount = moves.Count;

            //if (inCheck)                 84000 -> Time Ellapsed - 125387
            //    newDepth++;                       % 0.940635626616563      Evals - 115691008       Gens - 39131214 Eval / s = 922671 calls / s = 312083

            bool noneCaptures = false;
            bool PVmove = true;

            //Futility Pruning
            //if (depth == 1 && !inCheck && !PV && StaticEvaluation(board) + 200 <= alpha)
            //    return new Node { score = alpha };

            // Static Null2
            if (depth < 3 && !PV && !inCheck)
            {
                int[] margin = endGame ? new int[] { 60, 180, 340 } : new int[] { 0, 120, 240 }; // Safer (larger) margins?
                int staticEval = StaticEvaluation(board);
                if (staticEval - margin[depth] >= beta)
                    return new Node { score = staticEval - margin[depth], type = NodeType.StaticNull };
            }

            // Rank cut

            // Adaptive Null
            ///*
            if (depth > 2 && DO_NULL && !PV && !inCheck)
            {
                board.MakeNullMove();
                // if end game lower Reductions
                int R = depth > 6 ? 4 : 3;

                var val = -ST_NSS(depth - 1 - R, -beta, -beta + 1, false, false);

                board.TakeNullMove();

                if (val.score >= beta)
                {
                    newDepth -= 4;
                    if(newDepth <= 0)
                        return ST_QSearch(depth - 1, alpha, beta, true); //new Node { score = beta, type = NodeType.NullMove };
                }
            }
            //*/

            int b = beta;
            Node score = new Node { };

            if (moveCount == 0)
                noneCaptures = true;

            // Search captures
            if (search())
                goto Beta_cut;

            moves = genQuietMoves();
            moveScores = moves.scores;
            moveCount = moves.Count;

            if (moveCount == 0 && noneCaptures)
                return new Node { score = -32000 + depth };

            // Search quiet moves
            if (search())
                goto Beta_cut;

            // Update Hash Table
            if (TranspositionTable.ContainsKey(boardKey))
            {
                if (TranspositionTable[boardKey].reachedDepth < depth) // <= depth, just rewrite
                    TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { score = alpha, type = alpha > OAlpha ? NodeType.Valid : NodeType.UBound } }; // isSame? ->   score = alpha, type = OAlpha == alpha ? NodeType.UBound : NodeType.Valid
            }
            else
                TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { score = alpha, type = alpha > OAlpha ? NodeType.Valid : NodeType.UBound }});

            // return Result
            return new Node { score = alpha };

        // Beta Cut Off
        Beta_cut:
            if (TranspositionTable.ContainsKey(boardKey))
            {
                if (TranspositionTable[boardKey].reachedDepth < depth) // <= depth, just rewrite
                    TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { score = alpha, type = NodeType.LBound } };
            }
            else
                TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { score = alpha, type = NodeType.LBound } });

            return new Node { score = alpha, type = NodeType.CutNode };

            bool search()
            {
                for (int i = 0; i < moveCount; i++)
                { 
                    var moveId = getNextMove();


                    if (!board.MakeMove(moves[moveId])) {
                        Console.WriteLine("Broken: ini - " + InitialMove + "\t d = " + depth + "\t move - " + moves[moveId]);
                    }

                    score = -ST_NSS(newDepth, -b, -alpha, PVmove, DO_NULL);

                    board.TakeMove();

                    if (!PVmove && score.score > alpha && score.score < beta)
                    {
                        numberOfReserches++;
                        board.MakeMove(moves[moveId]);
                        score = -ST_NSS(newDepth, -beta, -score.score, true, DO_NULL);
                        board.TakeMove();
                    }

                    if (score.score > alpha)
                        alpha = score.score;

                    if (MoveSide == board.State.SideToMove.Side)
                    {
                        beta = Math.Min(beta, -Alpha);
                    }

                    if (alpha >= beta)
                    {
                        betaCut++;

                        if (i == 0)
                            betaCutFirstMove++;

                        return true;
                    }

                    b = alpha + 1;
                    PVmove = false;
                }
                return false;
            }

            int getNextMove()
            {
                int bestMove = 0;

                for (int i = 1; i < moveCount; i++)
                {
                    if (moveScores[i] > moveScores[bestMove])
                        bestMove = i;
                }

                moveScores[bestMove] = -99999;

                return bestMove;
            }
        }

        Node ST_QSearch(int d, int alpha, int beta, bool HashUpdate = false) {
            var standingPat = StaticEvaluation(board);

            if (d < -100)
                return new Node { score = standingPat };

            if (standingPat >= beta)
            {
                //if (HashUpdate && !TranspositionTable.ContainsKey(board.State.Key))
                //    TranspositionTable.Add(board.State.Key, new TTStructure { reachedDepth = 0, reachedNode = new Node { score = standingPat, type = NodeType.LBound } });

                return new Node { score = standingPat, type = NodeType.CutNode };
            }

            if (!endGame && standingPat + 1000 < alpha)
                return new Node { score = alpha };

            if (standingPat > alpha)
                alpha = standingPat;

            var OAlpha = alpha;

            var moves = genCaptureMoves();
            var moveScores = moves.scores;
            var moveCount = moves.Count;

            for (int i = 0; i < moveCount; i++)
            {
                var move = moves[getNextMove()];

                if (!endGame && !board.Position.InCheck && !move.IsPromotionMove() && (standingPat + 200 + pieceValues[(int)move.GetCapturedPiece().Type()] < alpha || board.badCapture(move)))
                    continue;

                board.MakeMove(move);
                var pIdx = board.PositionIndex;

                var score = -ST_QSearch(d - 1, -beta, -alpha);

                board.PositionIndex = pIdx;
                board.TakeMove();

                if (score.score > alpha)
                    alpha = score.score;

                if (alpha >= beta)
                {
                    betaCut++;

                    if (i == 0)
                        betaCutFirstMove++;

                    //if (HashUpdate && !TranspositionTable.ContainsKey(board.State.Key))
                    //    TranspositionTable.Add(board.State.Key, new TTStructure { reachedDepth = 0, reachedNode = new Node { score = alpha, type = NodeType.LBound } });

                    return new Node { score = alpha, type = NodeType.CutNode };
                }               
            }

            //if(HashUpdate && !TranspositionTable.ContainsKey(board.State.Key))
            //    TranspositionTable.Add(board.State.Key, new TTStructure { reachedDepth = 0, reachedNode = new Node { score = alpha, type = alpha > OAlpha ? NodeType.Valid : NodeType.UBound } });

            return new Node { score = alpha };

            int getNextMove()
            {
                int bestMove = 0;

                for (int i = 1; i < moveCount; i++)
                {
                    if (moveScores[i] > moveScores[bestMove])
                        bestMove = i;
                }

                moveScores[bestMove] = -99999;

                return bestMove;
            }
        }

        int NewNegaScout(int depth, int alpha, int beta)
        {
            MoveList moveList;

            if (depth == 0)
            {
                return StaticEvaluation(board);
            }
            if (0 == (moveList = genMoves()).Count)
                return -32000;
            else {
                var nMoves = moveList.Count;
                int b = beta;
                int bestScore = -99999;

                for (int i = 0; i < nMoves; i++) {

                    if (board.MakeMove(moveList[i]))
                    {

                        int score = -NewNegaScout(depth - 1, -b, -alpha);
                        if (score > alpha && score < beta && i > 0)
                        {
                            score = -NewNegaScout(depth - 1, -beta, -score);
                        }

                        bestScore = Math.Max(bestScore, score);
                        alpha = Math.Max(alpha, score);

                        board.TakeMove();

                        if (alpha >= beta)
                            return alpha;

                        b = alpha + 1;
                    }
                }

                return bestScore;
            }
        }

        int NegaMaxAB(int depth, int alpha, int beta)
        {
            MoveList moves;

            if (depth == 0)
                return StaticEvaluation(board);
            if (0 == (moves = genMoves()).Count)
                return -32000;
            else
            {
                int value = alpha;//int.MinValue + 10;

                foreach (var move in moves)
                {
                    board.MakeMove(move);

                    var t = -NegaMaxAB(depth - 1, -beta, -value); //  -Math.Max(value, alpha)
                    value = Math.Max(value, t);

                    board.TakeMove();

                    if (value >= beta)
                        return value;
                }

                return value;
            }
        }

        int ABB(int depth, int alpha, int beta) {
            MoveList moveList;

            if (depth == 0)
            {
                return board.State.SideToMove.Side == 0 ? StaticEvaluation(board) : -StaticEvaluation(board);
            }
            if (0 == (moveList = genMoves()).Count)
                return -32000;
            if (board.State.SideToMove.Side == 0)
            {
                int score = int.MinValue;

                foreach (var move in moveList) {
                    board.MakeMove(move);

                    score = Math.Max(score, ABB(depth - 1, alpha, beta));
                    alpha = Math.Max(alpha, score);

                    board.TakeMove();

                    if (alpha >= beta)
                        break;
                }

                return score;
            }
            else {
                int score = int.MaxValue;

                foreach (var move in moveList)
                {
                    board.MakeMove(move);

                    score = Math.Min(score, ABB(depth - 1, alpha, beta));
                    beta = Math.Min(beta, score);

                    board.TakeMove();

                    if (alpha >= beta)
                        break;
                }

                return score;
            }

        }
        
        int AB(int depth, int alpha, int beta)
        {
            MoveList moves;
            // TT off
            var boardKey = board.State.Key;//getKey();

            if (TranspositionTable.ContainsKey(boardKey)) {
                var storedValues = TranspositionTable[boardKey];
                if (storedValues.reachedDepth >= depth) {
                    TThits++;
                    return storedValues.reachedNode.score;
                }
            }

            int value = board.State.SideToMove.Side == 0 ? int.MinValue + 10 : int.MaxValue - 10;

            if (depth == 0)
            {
                return board.State.SideToMove.Side == 0 ? StaticEvaluation(board) : -StaticEvaluation(board);
            }
            if (0 == (moves = genMoves()).Count)
                return board.State.SideToMove.Side == 0  ? -32000 : 32000;// if white, if black +32000
            else if (board.State.SideToMove.Side == 0)
            {
                int nMoves = moves.Count;
                
                //int value = int.MinValue;

                //foreach (var move in moves)
                for (int i = 0; i < nMoves; i++)
                {
                    //if (board.MakeMove(move))
                    {
                        board.MakeMove(moves[i]);

                        value = Math.Max(value, AB(depth - 1, alpha, beta));
                        alpha = Math.Max(value, alpha);

                        board.TakeMove();

                        if (alpha < Alpha)
                            alpha = Alpha;

                        if (alpha >= beta)
                            break;
                    }
                    //else {
                    //    var t = 3;
                    //}
                }

                //return alpha;
            }
            else
            {
                int nMoves = moves.Count;
               
                for (int i = 0; i < nMoves; i++)
                {
                    board.MakeMove(moves[i]);

                    value = Math.Min(value, AB(depth - 1, alpha, beta));
                    //value = AB(depth - 1, alpha, beta);
                    beta = Math.Min(value, beta);

                    board.TakeMove();

                    if (alpha < Alpha)
                        alpha = Alpha;

                    if (alpha >= beta)
                        break;
                }

                //return beta;
            }

            ///*
            var Exists = TranspositionTable.ContainsKey(boardKey);
            
            if (!Exists)
            {
                //var Structure = new TTStructure { reachedDepth = depth, reachedNode = new Node { score = value, type = NodeType.CutNode } };
                //TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { score = value, type = NodeType.CutNode } });
                //TranspositionTable.AddOrUpdate(boardKey, Structure, (key, struct1) => struct1.reachedDepth >= depth ? struct1 : Structure); //Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { score = value, type = NodeType.CutNode } });
            }
            else
            {
                if (TranspositionTable[boardKey].reachedDepth < depth)
                {
                    TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { score = value, type = NodeType.CutNode } };
                }
            }
            //*/

            return value;
        }

        void HeapSort(int[,] arr, bool white)
        {
            int n = arr.GetLength(0);

            // Build heap (rearrange array) 
            for (int i = n / 2 - 1; i >= 0; i--)
                heapify(arr, n, i, white);

            // One by one extract an element from heap 
            for (int i = n - 1; i >= 0; i--)
            {
                // Move current root to end 
                int temp1 = arr[0, 0]; int temp2 = arr[0, 1];
                arr[0, 0] = arr[i, 0]; arr[0, 1] = arr[i, 1];
                arr[i, 0] = temp1; arr[i, 1] = temp2;

                // call max heapify on the reduced heap 
                heapify(arr, i, 0, white);
            }

            void heapify(int[,] _arr, int _n, int _i, bool _white)
            {
                int largest = _i; // Initialize largest as root 
                int l = 2 * _i + 1; // left = 2*i + 1 
                int r = 2 * _i + 2; // right = 2*i + 2 

                if (_white)
                {
                    if (l < _n && _arr[l, 1] < _arr[largest, 1])
                        largest = l;

                    if (r < _n && _arr[r, 1] < _arr[largest, 1])
                        largest = r;
                }
                else
                {
                    if (l < _n && _arr[l, 1] > _arr[largest, 1])
                        largest = l;

                    if (r < _n && _arr[r, 1] > _arr[largest, 1])
                        largest = r;
                }

                // If largest is not root 
                if (largest != _i)
                {
                    int swap1 = _arr[_i, 0]; int swap2 = _arr[_i, 1];
                    _arr[_i, 0] = _arr[largest, 0]; _arr[_i, 1] = _arr[largest, 1];
                    _arr[largest, 0] = swap1; _arr[largest, 1] = swap2;

                    // Recursively heapify the affected sub-tree 
                    heapify(_arr, _n, largest, _white);
                }
            }

        }
        
        Node PVS(int depth, int alpha, int beta)
        {
            MoveList moves;
            //TT off
            ulong BoardKey = board.State.Key;//getKey();

            if (TranspositionTable.ContainsKey(BoardKey))
            {
                var savedValues = TranspositionTable[BoardKey];
                if (savedValues.reachedDepth >= depth)
                {
                    TThits++;
                    return savedValues.reachedNode;
                }
            }

            if (depth == 0)
            {
                return new Node { score = StaticEvaluation(board), type = NodeType.ExactNode };
            }

            bool bSearchPv = true;

            if (0 == (moves = genMoves()).Count)
            {
                return new Node { score = -32000, type = NodeType.ExactNode };
            }

            {
                Node BestResult = new Node { score = -999999, type = NodeType.TTCutNode };
                Node result;

                foreach (var move in moves)
                {
                    if (board.MakeMove(move))
                    {
                        if (bSearchPv)
                        {
                            result = -PVS(depth - 1, -beta, -alpha);
                        }
                        else
                        {
                            result = -ZW_search(depth - 1, -alpha);
                            if (result.score > alpha && result.score < beta)
                            {
                                numberOfReserches++;
                                result = -PVS(depth - 1, -beta, -alpha);
                            }
                        }

                        board.TakeMove();

                        BestResult = maxNode(BestResult, result);

                        if (board.State.SideToMove.Side == MoveSide)
                        {
                            beta = Math.Min(beta, -Alpha);
                        }
                        else
                        {
                            alpha = Math.Max(alpha, Alpha);
                        }

                        if (result.score >= beta)
                        {
                            return new Node { score = beta, type = NodeType.CutNode };
                        }
                        if (result.score > alpha)
                        {
                            alpha = result.score;
                            bSearchPv = false;  //////////////////////////////////////////////////////////////
                        }
                    }
                }

                //if (!TranspositionTable.ContainsKey(BoardKey))
                    //TranspositionTable.Add(BoardKey, new TTStructure { reachedNode = BestResult, reachedDepth = depth });

                return BestResult;
            }
        }

        Node ZW_search(int depth, int beta) {
            MoveList moves;

            ulong BoardKey = board.State.Key;//getKey();

            if (TranspositionTable.ContainsKey(BoardKey))
            {
                var savedValues = TranspositionTable[BoardKey];
                if (savedValues.reachedDepth >= depth)
                {
                    TThits++;
                    return savedValues.reachedNode;
                }
            }

            if (depth == 0)
            {
                return new Node { score = StaticEvaluation(board), type = NodeType.ExactNode };
            }

            bool bSearchPv = true;

            var boardKey = board.State.Key;

            if (0 == (moves = genMoves()).Count)
            {
                return new Node { score = -32000, type = NodeType.ExactNode };
            }

            {
                Node BestResult = new Node { score = -999999, type = NodeType.TTCutNode };
                Node result;

                foreach (var move in moves)
                {
                    if (board.MakeMove(move))
                    {

                        result = -ZW_search(depth - 1, 1 - beta);

                        board.TakeMove();

                        if (result.score >= beta)
                            return new Node { score = beta, type = NodeType.CutNode};

                        //BestResult = maxNode(BestResult, result);
                    }
                }

                return new Node { score = beta - 1, type = NodeType.CutNode };
            }

        }

        bool rightId = false;

        int NegaScout(int depth, int alpha, int beta)
        {//TT off
            MoveList moves;
            var OAlpha = alpha;
            var boardKey = board.State.Key;//getKey();
            if (TranspositionTable.ContainsKey(boardKey))
            {
                var entry = TranspositionTable[boardKey];
                if (entry.reachedDepth >= depth)
                {
                    TThits++;

                    if (entry.reachedNode.type == NodeType.Valid)
                        return entry.reachedNode.score;
                    if (entry.reachedNode.type == NodeType.LBound)
                        alpha = Math.Max(alpha, entry.reachedNode.score);
                    if (entry.reachedNode.type == NodeType.UBound)
                        beta = Math.Min(beta, entry.reachedNode.score);
                    if (alpha >= beta)
                        return alpha;
                }
            }

            if (depth == 0)
            {
                return StaticEvaluation(board);//evaluateReletive(board);
            }

            bool bSearchPv = true;

            if (0 == (moves = genMoves()).Count)
                return -32000;
            else
            {
                int score = -9999999;
                var nMoves = moves.Count;

                for (int i = 0; i < nMoves; i++)
                {
                    if (board.MakeMove(moves[i])) // nMoves - i - 1 (reverse order)
                    {
                        if (bSearchPv)
                        {
                            score = -NegaScout(depth - 1, -beta, -alpha);
                        }
                        else
                        {
                            score = -NegaScout(depth - 1, -alpha - 1, -alpha);
                            if (score > alpha && score < beta) //  && score < beta
                            {
                                numberOfReserches++;
                                score = -NegaScout(depth - 1, -beta, -alpha);
                            }
                        }

                        board.TakeMove();

                        if (score >= beta)
                        {
                            if (TranspositionTable.ContainsKey(boardKey))
                            {
                                var entry = TranspositionTable[boardKey];
                                if (entry.reachedDepth < depth)
                                    TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { type = NodeType.LBound, score = score } }; // score = score
                            }
                            //else
                                //TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { type = NodeType.LBound, score = score } }); // score = score

                            return beta;
                        }
                        if (score > alpha)
                        {
                            alpha = score;
                            bSearchPv = false;
                        }
                        bSearchPv = false;
                    }
                }


                if (TranspositionTable.ContainsKey(boardKey))
                {
                    var entry = TranspositionTable[boardKey];
                    if (entry.reachedDepth < depth)
                        TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { type = alpha > OAlpha ? NodeType.Valid : NodeType.UBound, score = alpha } };
                }
                //else
                    //TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { type = alpha > OAlpha ? NodeType.Valid : NodeType.UBound, score = alpha } });

                return alpha;
            }

        }

        Node NodeNegaScout(int depth, int alpha, int beta, bool nullMoveSearch)
        {
            /*
            if (TranspositionTable.ContainsKey(board.State.Key))
            {
                var entry = TranspositionTable[board.State.Key];
                if (entry.reachedDepth >= depth)
                {
                    TThits++;

                    if (entry.reachedNode.type == NodeType.Valid)
                        return entry.reachedNode;
                    if (entry.reachedNode.type == NodeType.LBound)
                        alpha = Math.Max(alpha, entry.reachedNode.score);
                    if (entry.reachedNode.type == NodeType.UBound)
                        beta = Math.Min(beta, entry.reachedNode.score);
                    if (alpha >= beta)
                        return new Node { score = alpha, type = NodeType.CutNode };
                }
            }
            //*/

            if (depth <= 0)
                return new Node { score = StaticEvaluation(board), type = NodeType.ExactNode };

            // Null Move Reduction
            /*
            if (!nullMoveSearch && depth > 2 && depth < depthToSearch && !board.Position.InCheck)
            {
                const int R1 = 2, R2 = 3;

                board.MakeNullMove();//board.State.SideToMove = ~board.State.SideToMove;
                int nullScore = -NodeNegaScout(depth - 1 - (depth > 6 ? R2 : R1), -beta, -beta + 1, true).score;
                board.TakeNullMove();//board.State.SideToMove = ~board.State.SideToMove;

                if (nullScore >= beta)
                {
                    depth -= 3; 
                    if (depth <= 0)
                        return new Node { score = StaticEvaluation(board), type = NodeType.ExactNode };
                }
            }
            //*/


            MoveList moves = genMoves();
            if (0 == moves.Count)
                return new Node { score = -32000 + depth, type = NodeType.ExactNode };

            int nMoves = moves.Count;

            int OAlpha = alpha;
            ulong boardKey = board.State.Key;

            bool bSearchPv = true;
            Node score = new Node { score = -9999999 };

            for (int i = 0; i < nMoves; i++)
            {
                {
                    if (bSearchPv)
                    {
                        board.MakeMove(moves[i]);
                        score = -NodeNegaScout(depth - 1, -beta, -alpha, nullMoveSearch);
                        board.TakeMove();
                    }
                    else
                    {
                        board.MakeMove(moves[i]);
                        score = -NodeNegaScout(depth - 1, -alpha - 1, -alpha, nullMoveSearch);
                        board.TakeMove();
                        if (score.score > alpha && score.score < beta)
                        {
                            numberOfReserches++;
                            board.MakeMove(moves[i]);
                            score = -NodeNegaScout(depth - 1, -beta, -alpha, nullMoveSearch);
                            board.TakeMove();
                        }
                    }

                    if (MoveSide == board.State.SideToMove.Side)
                    {
                        beta = Math.Min(beta, -Alpha);
                    }

                    if (score.score >= beta)
                    {

                        if (TranspositionTable.ContainsKey(boardKey))
                        {
                            var entry = TranspositionTable[boardKey];
                            if (entry.reachedDepth < depth)
                                TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { type = NodeType.LBound, score = score.score } }; // score = score
                        }
                        else
                            TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { type = NodeType.LBound, score = score.score } }); // score = score

                        return new Node { score = beta, type = NodeType.CutNode };
                    }
                    if (score.score > alpha)
                    {
                        alpha = score.score;
                    }
                    bSearchPv = false;
                }
            }


            if (TranspositionTable.ContainsKey(boardKey))
            {
                var entry = TranspositionTable[boardKey];
                if (entry.reachedDepth < depth)
                    TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { type = alpha > OAlpha ? NodeType.Valid : NodeType.UBound, score = alpha } };
            }
            else
                TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { type = alpha > OAlpha ? NodeType.Valid : NodeType.UBound, score = alpha } });

            return new Node { score = alpha };
            

        }

        Node NodeNegaScout_QSearch(int depth, int alpha, int beta, bool doNullMove)
        {
            if (TranspositionTable.ContainsKey(board.State.Key))
            {
                var entry = TranspositionTable[board.State.Key];
                if (entry.reachedDepth >= depth)
                {
                    TThits++;

                    if (entry.reachedNode.type == NodeType.Valid)
                        return entry.reachedNode;
                    if (entry.reachedNode.type == NodeType.LBound)
                        alpha = Math.Max(alpha, entry.reachedNode.score);
                    if (entry.reachedNode.type == NodeType.UBound)
                        beta = Math.Min(beta, entry.reachedNode.score);
                    if (alpha >= beta)
                        return new Node { score = alpha, type = NodeType.CutNode };
                }
            }

            /*
            if (doNullMove && depth < depthToSearch + QSearchLimit && !board.Position.InCheck)
            {
                board.MakeNullMove();

                int nullScore = -NodeNegaScout_QSearch(depth - 4, -beta, -beta + 1, false).score;

                board.TakeNullMove();

                if (nullScore >= beta)
                    depth -= 3;//return new Node { score = beta };
            }
            //*/

            if (depth <= QSearchLimit) 
            {
                return NewQSearch(depth, alpha, beta);//QSearch(depth, alpha, beta);
                //return new Node { score = StaticEvaluation(board), type = NodeType.ExactNode };
            }

            var OAlpha = alpha;
            var boardKey = board.State.Key;//getKey();

            bool bSearchPv = true;

            MoveList moves = genMoves();
            
            if (0 == moves.Count)
                return new Node { score = -32000 + depth, type = NodeType.ExactNode };
            else
            {
                Node score = new Node { score = -9999999 };
                var nMoves = moves.Count;
                int[] moveScores = moves.scores;

                int getNextMove()
                {
                    int bestMove = 0;

                    for (int i = 1; i < nMoves; i++)
                    {
                        if (moveScores[i] > moveScores[bestMove])
                            bestMove = i;
                    }

                    moveScores[bestMove] = -99999;

                    return bestMove;
                }

                for (int i = 0; i < nMoves; i++)
                {
                    //if (board.MakeMove(moves[i])) // nMoves - i - 1 (reverse order)
                    {
                        var move = moves[getNextMove()];
                        if (bSearchPv)
                        {
                            board.MakeMove(move);
                            score = -NodeNegaScout_QSearch(depth - 1, -beta, -alpha, doNullMove);
                            board.TakeMove();
                        }
                        else
                        {
                            board.MakeMove(move);
                            score = -NodeNegaScout_QSearch(depth - 1, -alpha - 1, -alpha, doNullMove);
                            board.TakeMove();
                            if (score.score > alpha && score.score < beta) //  && score < beta
                            {
                                numberOfReserches++;
                                board.MakeMove(move);
                                score = -NodeNegaScout_QSearch(depth - 1, -beta, -alpha, doNullMove);
                                board.TakeMove();
                            }
                        }

                        if (MoveSide == board.State.SideToMove.Side)
                        {
                            beta = Math.Min(beta, -Alpha);
                        }

                        //if (i == 0)
                            //firstMove++;

                        if (score.score >= beta)
                        {
                            if (i == 0)
                                betaCutFirstMove++;


                            betaCut++;

                            if (TranspositionTable.ContainsKey(boardKey))
                            {
                                var entry = TranspositionTable[boardKey];
                                if (entry.reachedDepth < depth)
                                    TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { type = NodeType.LBound, score = score.score } }; // score = score
                            }
                            else
                                TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { type = NodeType.LBound, score = score.score } }); // score = score
                            
                            return new Node { score = beta, type = NodeType.CutNode};
                        }
                        if (score.score > alpha)
                        {
                            alpha = score.score;
                        }
                        bSearchPv = false;
                    }
                }

                
                if (TranspositionTable.ContainsKey(boardKey))
                {
                    var entry = TranspositionTable[boardKey];
                    if (entry.reachedDepth < depth)
                        TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { type = alpha > OAlpha ? NodeType.Valid : NodeType.UBound, score = alpha } };
                }
                else
                    TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { type = alpha > OAlpha ? NodeType.Valid : NodeType.UBound, score = alpha } });
               
                return new Node { score = alpha };
            }

        }

        Node QSearch(int depth, int alpha, int beta) {
            if (TranspositionTable.ContainsKey(board.State.Key))
            {
                var entry = TranspositionTable[board.State.Key];
                if (entry.reachedDepth >= depth)
                {
                    TThits++;

                    if (entry.reachedNode.type == NodeType.Valid)
                        return entry.reachedNode;
                    if (entry.reachedNode.type == NodeType.LBound)
                        alpha = Math.Max(alpha, entry.reachedNode.score);
                    if (entry.reachedNode.type == NodeType.UBound)
                        beta = Math.Min(beta, entry.reachedNode.score);
                    if (alpha >= beta)
                        return new Node { score = alpha, type = NodeType.CutNode };
                }
            }

            var OAlpha = alpha;
            var boardKey = board.State.Key;//getKey();

            bool bSearchPv = true;

            MoveList moves = genMoves();

            if (0 == moves.Count)
                return new Node { score = -32000 + depth, type = NodeType.ExactNode };
            else
            {
                Node score = new Node { score = -9999999 };
                var nMoves = moves.Count;

                for (int i = 0; i < nMoves; i++)
                {
                    //if (board.MakeMove(moves[i])) // nMoves - i - 1 (reverse order)
                    {
                        if (bSearchPv)
                        {
                            board.MakeMove(moves[i]);
                            score = new Node { score = -StaticEvaluation(board), type = NodeType.ExactNode };
                            board.TakeMove();
                        }
                        else
                        {
                            board.MakeMove(moves[i]);
                            score = new Node { score = -StaticEvaluation(board), type = NodeType.ExactNode };
                            board.TakeMove();
                        }

                        if (score.score >= beta)
                        {
                            
                            if (TranspositionTable.ContainsKey(boardKey))
                            {
                                var entry = TranspositionTable[boardKey];
                                if (entry.reachedDepth < depth)
                                    TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { type = NodeType.LBound, score = score.score } }; // score = score
                            }
                            else
                                TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { type = NodeType.LBound, score = score.score } }); // score = score
                            
                            return new Node { score = beta, type = NodeType.CutNode };
                        }
                        if (score.score > alpha)
                        {
                            alpha = score.score;
                        }
                        bSearchPv = false;
                    }
                }

                
                if (TranspositionTable.ContainsKey(boardKey))
                {
                    var entry = TranspositionTable[boardKey];
                    if (entry.reachedDepth < depth)
                        TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { type = alpha > OAlpha ? NodeType.Valid : NodeType.UBound, score = alpha } };
                }
                else
                    TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { type = alpha > OAlpha ? NodeType.Valid : NodeType.UBound, score = alpha } });
                
                
                return new Node { score = alpha };
            }
        }

        Node NewQSearch(int depth, int alpha, int beta)
        {
            /*
            if (TranspositionTable.ContainsKey(board.State.Key))
            {
                var entry = TranspositionTable[board.State.Key];
                if (entry.reachedDepth >= depth)
                {
                    TThits++;

                    if (entry.reachedNode.type == NodeType.Valid)
                        return entry.reachedNode;
                    if (entry.reachedNode.type == NodeType.LBound)
                        alpha = Math.Max(alpha, entry.reachedNode.score);
                    if (entry.reachedNode.type == NodeType.UBound)
                        beta = Math.Min(beta, entry.reachedNode.score);
                    if (alpha >= beta)
                        return new Node { score = alpha, type = NodeType.CutNode };
                }
            }
            */

            Node node = new Node { score = StaticEvaluation(board) };
            var boardKey = board.State.Key;

            if (node.score >= beta)
            {
                /*
                if (TranspositionTable.ContainsKey(boardKey))
                {
                    var entry = TranspositionTable[boardKey];
                    if (entry.reachedDepth < depth)
                        TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { type = NodeType.LBound, score = node.score } }; // score = score
                }
                else
                    TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { type = NodeType.LBound, score = node.score } }); // score = score
                    //*/
                return new Node { score = beta };
            }
            else if (alpha < node.score)
                alpha = node.score;

            if (depth == 0)
                return node;

            var OAlpha = alpha;
            var moves = genCaptureMoves();
            var nMoves = moves.Count;

            int[] moveScores = moves.scores;

            int getNextMove()
            {
                int bestMove = 0;

                for (int i = 1; i < nMoves; i++)
                {
                    if (moveScores[i] > moveScores[bestMove])
                        bestMove = i;
                }

                moveScores[bestMove] = -99999;

                return bestMove;
            }

            for (int i = 0; i < nMoves; i++)
            {
                var move = moves[getNextMove()];

                board.MakeMove(move);

                var result = -NewQSearch(depth -1, - beta, -alpha);

                board.TakeMove();

                //if (i == 0)
                    //firstMove++;

                if (result.score >= beta)
                {
                    if (i == 0)
                        betaCutFirstMove++;


                    betaCut++;

                    /*
                    if (TranspositionTable.ContainsKey(boardKey))
                    {
                        var entry = TranspositionTable[boardKey];
                        if (entry.reachedDepth < depth)
                            TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { type = NodeType.LBound, score = result.score } }; // score = score
                    }
                    else
                        TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { type = NodeType.LBound, score = result.score } }); // score = score
                    */

                    return new Node { score = beta };
                }

                if (alpha < result.score)
                    alpha = result.score;
            }
            /*
            if (TranspositionTable.ContainsKey(boardKey))
            {
                var entry = TranspositionTable[boardKey];
                if (entry.reachedDepth < depth)
                    TranspositionTable[boardKey] = new TTStructure { reachedDepth = depth, reachedNode = new Node { type = alpha > OAlpha ? NodeType.Valid : NodeType.UBound, score = alpha } };
            }
            else
                TranspositionTable.Add(boardKey, new TTStructure { reachedDepth = depth, reachedNode = new Node { type = alpha > OAlpha ? NodeType.Valid : NodeType.UBound, score = alpha } });
            */
            return new Node { score = alpha };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Node maxNode(Node node1, Node node2) {
            if (node1.score >= node2.score)
            {
                return node1;
            }
            else {
                return node2;
            }
        }

        #endregion

        #region Evaluation routine

        ulong[] Files = new ulong[] { 72340172838076673, 144680345676153346, 289360691352306692, 578721382704613384, 1157442765409226768, 2314885530818453536, 4629771061636907072, 9259542123273814144 };
        ulong[] Ranks = new ulong[] { 255, 65280, 16711680, 4278190080, 1095216660480, 280375465082880, 71776119061217280, 18374686479671623680 };

        short[] PawnTable = new short[]
        {
             0,  0,  0,  0,  0,  0,  0,  0,
            50, 50, 50, 50, 50, 50, 50, 50,
            20, 20, 30, 40, 40, 30, 20, 20,
             5,  5, 10, 30, 30, 10,  5,  5,
             0,  0,  0, 25, 25,  0,  0,  0,
             5, -5,-10,  0,  0,-10, -5,  5,
             5, 10, 10,-30,-30, 10, 10,  5,
             0,  0,  0,  0,  0,  0,  0,  0
        };

        short[] KnightTable = new short[]
        {
            -50,-40,-30,-30,-30,-30,-40,-50,
            -40,-20,  0,  0,  0,  0,-20,-40,
            -30,  0, 10, 15, 15, 10,  0,-30,
            -30,  5, 15, 20, 20, 15,  5,-30,
            -30,  0, 15, 20, 20, 15,  0,-30,
            -30,  5, 10, 15, 15, 10,  5,-30,
            -40,-20,  0,  5,  5,  0,-20,-40,
            -50,-30,-20,-30,-30,-20,-30,-50,
        };

        short[] BishopTable = new short[]
        {
            -20,-10,-10,-10,-10,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5, 10, 10,  5,  0,-10,
            -10,  5,  5, 10, 10,  5,  5,-10,
            -10,  0, 10, 10, 10, 10,  0,-10,
            -10, 10, 10, 10, 10, 10, 10,-10,
            -10,  5,  0,  0,  0,  0,  5,-10,
            -20,-10,-40,-10,-10,-40,-10,-20,
        };

        short[] KingTable = new short[]
        {
          -30, -40, -40, -50, -50, -40, -40, -30,
          -30, -40, -40, -50, -50, -40, -40, -30,
          -30, -40, -40, -50, -50, -40, -40, -30,
          -30, -40, -40, -50, -50, -40, -40, -30,
          -20, -30, -30, -40, -40, -30, -30, -20,
          -10, -20, -20, -20, -20, -20, -20, -10,
           20,  20,   0,   0,   0,   0,  20,  20,
           20,  30,  10,   0,   0,  10,  30,  20
        };

        short[] KingTableEndGame = new short[]
        {
            -50,-40,-30,-20,-20,-30,-40,-50,
            -30,-20,-10,  0,  0,-10,-20,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-30,  0,  0,  0,  0,-30,-30,
            -50,-30,-30,-30,-30,-30,-30,-50
        };

        ulong[] IsolatedPawns64 = new ulong[] {
                144680345676153346, 361700864190383365, 723401728380766730, 1446803456761533460, 2893606913523066920, 5787213827046133840, 11574427654092267680, 4629771061636907072,
                144680345676153346, 361700864190383365, 723401728380766730, 1446803456761533460, 2893606913523066920, 5787213827046133840, 11574427654092267680, 4629771061636907072,
                144680345676153346, 361700864190383365, 723401728380766730, 1446803456761533460, 2893606913523066920, 5787213827046133840, 11574427654092267680, 4629771061636907072,
                144680345676153346, 361700864190383365, 723401728380766730, 1446803456761533460, 2893606913523066920, 5787213827046133840, 11574427654092267680, 4629771061636907072,
                144680345676153346, 361700864190383365, 723401728380766730, 1446803456761533460, 2893606913523066920, 5787213827046133840, 11574427654092267680, 4629771061636907072,
                144680345676153346, 361700864190383365, 723401728380766730, 1446803456761533460, 2893606913523066920, 5787213827046133840, 11574427654092267680, 4629771061636907072,
                144680345676153346, 361700864190383365, 723401728380766730, 1446803456761533460, 2893606913523066920, 5787213827046133840, 11574427654092267680, 4629771061636907072,
                144680345676153346, 361700864190383365, 723401728380766730, 1446803456761533460, 2893606913523066920, 5787213827046133840, 11574427654092267680, 4629771061636907072
            };

        ulong[][] PawnOpenFile = new ulong[][] { 
            new ulong[] { 72340172838076672, 144680345676153344, 289360691352306688, 578721382704613376, 1157442765409226752, 2314885530818453504, 4629771061636907008, 9259542123273814016,
                          72340172838076416, 144680345676152832, 289360691352305664, 578721382704611328, 1157442765409222656, 2314885530818445312, 4629771061636890624, 9259542123273781248,
                          72340172838010880, 144680345676021760, 289360691352043520, 578721382704087040, 1157442765408174080, 2314885530816348160, 4629771061632696320, 9259542123265392640,
                          72340172821233664, 144680345642467328, 289360691284934656, 578721382569869312, 1157442765139738624, 2314885530279477248, 4629771060558954496, 9259542121117908992,
                          72340168526266368, 144680337052532736, 289360674105065472, 578721348210130944, 1157442696420261888, 2314885392840523776, 4629770785681047552, 9259541571362095104,
                          72339069014638592, 144678138029277184, 289356276058554368, 578712552117108736, 1157425104234217472, 2314850208468434944, 4629700416936869888, 9259400833873739776,
                          72057594037927936, 144115188075855872, 288230376151711744, 576460752303423488, 1152921504606846976, 2305843009213693952, 4611686018427387904, 9223372036854775808,
                          0, 0, 0, 0, 0, 0, 0, 0, },
            new ulong[] { 0, 0, 0, 0, 0, 0, 0, 0,
                          1, 2, 4, 8, 16, 32, 64, 128, 257,
                          514, 1028, 2056, 4112, 8224, 16448,
                          32896, 65793, 131586, 263172, 526344,
                          1052688, 2105376, 4210752, 8421504, 16843009,
                          33686018, 67372036, 134744072, 269488144, 538976288,
                          1077952576, 2155905152, 4311810305, 8623620610, 17247241220,
                          34494482440, 68988964880, 137977929760, 275955859520, 551911719040,
                          1103823438081, 2207646876162, 4415293752324, 8830587504648, 17661175009296,
                          35322350018592, 70644700037184, 141289400074368, 282578800148737, 565157600297474,
                          1130315200594948, 2260630401189896, 4521260802379792, 9042521604759584, 18085043209519168, 36170086419038336 } };
        ulong[][] PassedPawns = new ulong[][]{
            new ulong[] {
                217020518514230016, 506381209866536704, 1012762419733073408, 2025524839466146816, 4051049678932293632, 8102099357864587264, 16204198715729174528, 13889313184910721024,
                217020518514229248, 506381209866534912, 1012762419733069824, 2025524839466139648, 4051049678932279296, 8102099357864558592, 16204198715729117184, 13889313184910671872,
                217020518514032640, 506381209866076160, 1012762419732152320, 2025524839464304640, 4051049678928609280, 8102099357857218560, 16204198715714437120, 13889313184898088960,
                217020518463700992, 506381209748635648, 1012762419497271296, 2025524838994542592, 4051049677989085184, 8102099355978170368, 16204198711956340736, 13889313181676863488,
                217020505578799104, 506381179683864576, 1012762359367729152, 2025524718735458304, 4051049437470916608, 8102098874941833216, 16204197749883666432, 13889312357043142656,
                217017207043915776, 506373483102470144, 1012746966204940288, 2025493932409880576, 4050987864819761152, 8101975729639522304, 16203951459279044608, 13889101250810609664,
                216172782113783808, 504403158265495552, 1008806316530991104, 2017612633061982208, 4035225266123964416, 8070450532247928832, 16140901064495857664, 13835058055282163712,
                0, 0, 0, 0, 0, 0, 0, 0 },
            new ulong[] {
                0, 0, 0, 0, 0, 0, 0, 0,
                3, 7, 14, 28, 56, 112, 224, 192, 771,
                1799, 3598, 7196, 14392, 28784, 57568,
                49344, 197379, 460551, 921102, 1842204,
                3684408, 7368816, 14737632, 12632256, 50529027,
                117901063, 235802126, 471604252, 943208504, 1886417008, 3772834016,
                3233857728, 12935430915, 30182672135, 60365344270, 120730688540, 241461377080, 482922754160,
                965845508320, 827867578560, 3311470314243, 7726764066567, 15453528133134, 30907056266268, 61814112532536,
                123628225065072, 247256450130144, 211934100111552, 847736400446211, 1978051601041159, 3956103202082318, 7912206404164636, 15824412808329272, 31648825616658544, 63297651233317088, 54255129628557504 }};

        ulong[][] PawnEastAttack = new ulong[][] {
            new ulong[] {
                0, 0, 0, 0, 0, 0, 0, 0,
                131072, 262144, 524288, 1048576, 2097152, 4194304, 8388608, 0,
                33554432, 67108864, 134217728, 268435456, 536870912, 1073741824, 2147483648, 0,
                8589934592, 17179869184, 34359738368, 68719476736, 137438953472, 274877906944, 549755813888, 0,
                2199023255552, 4398046511104, 8796093022208, 17592186044416, 35184372088832, 70368744177664, 140737488355328, 0,
                562949953421312, 1125899906842624, 2251799813685248, 4503599627370496, 9007199254740992, 18014398509481984, 36028797018963968, 0,
                144115188075855872, 288230376151711744, 576460752303423488, 1152921504606846976, 2305843009213693952, 4611686018427387904, 9223372036854775808,
                0, 0, 0, 0, 0, 0, 0, 0, 0 },
            new ulong[] {
                0, 0, 0, 0, 0, 0, 0, 0,
                2, 4, 8, 16, 32, 64, 128, 0,
                512, 1024, 2048, 4096, 8192, 16384, 32768, 0,
                131072, 262144, 524288, 1048576, 2097152, 4194304, 8388608,
                0, 33554432, 67108864, 134217728, 268435456, 536870912, 1073741824, 2147483648,
                0, 8589934592, 17179869184, 34359738368, 68719476736, 137438953472, 274877906944, 549755813888,
                0, 2199023255552, 4398046511104, 8796093022208, 17592186044416, 35184372088832, 70368744177664, 140737488355328,
                0, 0, 0, 0, 0, 0, 0, 0, 0, } };
        ulong[][] PawnWestAttack = new ulong[][]{
            new ulong[]{
                0, 0, 0, 0, 0, 0, 0, 0, 0,
                65536, 131072, 262144, 524288, 1048576, 2097152, 4194304, 0,
                16777216, 33554432, 67108864, 134217728, 268435456, 536870912, 1073741824, 0,
                4294967296, 8589934592, 17179869184, 34359738368, 68719476736, 137438953472, 274877906944, 0,
                1099511627776, 2199023255552, 4398046511104, 8796093022208, 17592186044416, 35184372088832, 70368744177664, 0,
                281474976710656, 562949953421312, 1125899906842624, 2251799813685248, 4503599627370496, 9007199254740992, 18014398509481984, 0,
                72057594037927936, 144115188075855872, 288230376151711744, 576460752303423488, 1152921504606846976, 2305843009213693952, 4611686018427387904,
                0, 0, 0, 0, 0, 0, 0, 0 },
            new ulong[]{
                0, 0, 0, 0, 0, 0, 0, 0, 0,
                1, 2, 4, 8, 16, 32, 64, 0,
                256, 512, 1024, 2048, 4096, 8192, 16384, 0,
                65536, 131072, 262144, 524288, 1048576, 2097152, 4194304, 0,
                16777216, 33554432, 67108864, 134217728, 268435456, 536870912, 1073741824, 0,
                4294967296, 8589934592, 17179869184, 34359738368, 68719476736, 137438953472, 274877906944, 0,
                1099511627776, 2199023255552, 4398046511104, 8796093022208, 17592186044416, 35184372088832, 70368744177664,
                0, 0, 0, 0, 0, 0, 0, 0, }};

        ulong[][] KingSafetyBitBoard = new ulong[][]{
            new ulong[]{ 197376, 460544, 921088, 1842176, 3684352, 7368704, 14737408, 12632064,
                           50528256, 117899264, 235798528, 471597056, 943194112, 1886388224, 3772776448, 3233808384,
                           12935233536, 30182211584, 60364423168, 120728846336, 241457692672, 482915385344, 965830770688, 827854946304,
                           3311419785216, 7726646165504, 15453292331008, 30906584662016, 61813169324032, 123626338648064, 247252677296128, 211930866253824,
                           847723465015296, 1978021418369024, 3956042836738048, 7912085673476096, 15824171346952192, 31648342693904384, 63296685387808768, 54254301760978944,
                           217017207043915776, 506373483102470144, 1012746966204940288, 2025493932409880576, 4050987864819761152, 8101975729639522304, 16203951459279044608, 13889101250810609664,
                           0, 0, 0, 0, 0, 0, 0, 0,
                           0, 0, 0, 0, 0, 0, 0, 0 },
            new ulong[]{ 0, 0, 0, 0, 0, 0, 0, 0, 0,
                            0, 0, 0, 0, 0, 0, 0,
                            771, 1799, 3598, 7196, 14392, 28784, 57568, 49344,
                            197376, 460544, 921088, 1842176, 3684352, 7368704, 14737408, 12632064,
                            50528256, 117899264, 235798528, 471597056, 943194112, 1886388224, 3772776448, 3233808384,
                            12935233536, 30182211584, 60364423168, 120728846336, 241457692672, 482915385344, 965830770688, 827854946304,
                            3311419785216, 7726646165504, 15453292331008, 30906584662016, 61813169324032, 123626338648064, 247252677296128, 211930866253824,
                            847723465015296, 1978021418369024, 3956042836738048, 7912085673476096, 15824171346952192, 31648342693904384, 63296685387808768, 54254301760978944 }};

        short[] KingSafetyBonus = new short[] { 5, 10, 30, 30, 35, 40 };

        //readonly ulong[] IsolatedPawns8 = new ulong[] { 144680345676153346, 361700864190383365, 723401728380766730, 1446803456761533460, 2893606913523066920, 5787213827046133840, 11574427654092267680, 4629771061636907072 };

        int StaticEvaluation(IGame game)
        {
            //////////// Feature Weights ////////////
            const int PawnValue = 100;
            const int KnightValue = 320;
            const int BishopValue = 370;
            const int RookValue = 600;
            const int QueenValue = 1100;

            const int KnightPair = 10;
            const int BishopPair = 30;
            const int RookPair = 40;

            const int OpenPawn = 15;
            const int PassedPawn = 40;
            const int DefendedPawn = 15;
            const int BakcwardPawn = -15;
            //const int AttackPawn = 0;

            const int Rook7Rank = 20;
            const int RooksDefence = 10;

            const int IsolatedPawnPenalty = -25;
            //////////// Cnt Info ////////////
            int MovingSide = board.State.SideToMove.Side;
            ulong PawnKey = board.State.PawnStrutureHashKey;

            int score = 0;
            byte bitIndex = 0;
            byte PieceCnt = 0;
            ulong value;


            ulong WhitePawns = game.Position.BoardPieces[1].Value;
            ulong BlackPawns = game.Position.BoardPieces[9].Value;

            if (PawnEvaluationTable.ContainsKey(PawnKey))
            {
                score += PawnEvaluationTable[PawnKey];
            }
            else
            {
                #region // White Pawn

                value = WhitePawns;
                while (value != 0)
                {
                    bitIndex = LeastSignificantBit(value);

                    // Isolated Pawns
                    if ((WhitePawns & IsolatedPawns64[bitIndex]) == 0)
                        score += IsolatedPawnPenalty;
                    // Open & Passed Pawns
                    if ((BlackPawns & PawnOpenFile[0][bitIndex]) == 0)
                    {
                        score += OpenPawn;
                        if ((BlackPawns & PassedPawns[0][bitIndex]) == 0)
                            score += PassedPawn;
                    }
                    // Defending Pawns
                    if ((PawnEastAttack[0][bitIndex] & WhitePawns) != 0)
                        score += DefendedPawn;
                    else if ((PawnWestAttack[0][bitIndex] & WhitePawns) != 0)
                        score += DefendedPawn;
                    // BackwardPawn
                    if ((WhitePawns & PassedPawns[1][bitIndex]) == 0)
                        score += BakcwardPawn;

                    value &= value - 1;// ^= 1UL << bitIndex; // &= ~(1UL << bitIndex);

                    score += PawnValue + PawnTable[bitIndex];
                }
                #endregion

                #region // Black Pawn

                value = BlackPawns;
                while (value != 0)
                {
                    bitIndex = LeastSignificantBit(value);

                    // Isolated Pawns
                    if ((BlackPawns & IsolatedPawns64[bitIndex]) == 0)
                        score -= IsolatedPawnPenalty;
                    // Open & Passed Pawns
                    if ((WhitePawns & PawnOpenFile[1][bitIndex]) == 0)
                    {
                        score -= OpenPawn;
                        if ((WhitePawns & PassedPawns[1][bitIndex]) == 0)
                        {
                            score -= PassedPawn;
                        }
                    }
                    // Defending Pawns
                    if ((PawnEastAttack[1][bitIndex] & BlackPawns) != 0)
                        score -= DefendedPawn;
                    if ((PawnWestAttack[1][bitIndex] & BlackPawns) != 0)
                        score -= DefendedPawn;
                    // BackwardPawn
                    if ((BlackPawns & PassedPawns[0][bitIndex]) == 0)
                        score -= BakcwardPawn;

                    value &= value - 1;// ^= 1UL << bitIndex; // &= ~(1UL << bitIndex);

                    score -= PawnValue + PawnTable[63 - bitIndex]; //63 - bitIndex
                }
                #endregion

                PawnEvaluationTable.Add(PawnKey, score);
            }

            // Tempo
            score += MovingSide == 1 ? -10 : 10;

            // Check Penalty
            if (board.Position.InCheck)
                score -= 30;

            #region White King

            bitIndex = LeastSignificantBit(game.Position.BoardPieces[6].Value);

            if (!endGame)
            {
                value = WhitePawns & KingSafetyBitBoard[0][bitIndex];

                while (value != 0)
                {
                    value &= value - 1;
                    PieceCnt++;
                }

                score += KingTable[bitIndex];
                score += KingSafetyBonus[PieceCnt];
            }
            else
                score += KingTableEndGame[bitIndex];

            PieceCnt = 0;
            #endregion

            #region Black King

            bitIndex = LeastSignificantBit(game.Position.BoardPieces[14].Value);

            if (!endGame)
            {
                value = BlackPawns & KingSafetyBitBoard[1][bitIndex];

                while (value != 0)
                {
                    value &= value - 1;
                    PieceCnt++;
                }

                score -= KingTable[63 - bitIndex];
                score -= KingSafetyBonus[PieceCnt];
            }
            else
                score -= KingTableEndGame[63 - bitIndex];

            PieceCnt = 0;
            #endregion

            bool PieceFound = false;

            #region // White Knight
            value = game.Position.BoardPieces[2].Value;
            while (value != 0)
            {
                bitIndex = LeastSignificantBit(value);
                value &= value - 1;// ^= 1UL << bitIndex; // &= ~(1UL << bitIndex);
                
                if (PieceFound)
                    score += KnightPair;
                PieceFound = true;

                score += KnightValue + KnightTable[bitIndex];
            }

            #endregion

            #region // White Bishop

            PieceFound = false;
            value = game.Position.BoardPieces[3].Value;
            while (value != 0)
            {
                bitIndex = LeastSignificantBit(value);
                value &= value - 1;// ^= 1UL << bitIndex; // &= ~(1UL << bitIndex);
                
                if (PieceFound)
                    score += BishopPair;
                PieceFound = true;

                score += BishopValue + BishopTable[bitIndex];
            }

            #endregion

            int RooksFile = -1, RooksRank = -1;

            #region // White Rook

            PieceFound = false;
            value = game.Position.BoardPieces[4].Value;
            while (value != 0)
            {
                bitIndex = LeastSignificantBit(value);
                value &= value - 1;// ^= 1UL << bitIndex; // &= ~(1UL << bitIndex);

                if (PieceFound)
                {
                    score += RookPair;
                    if ((bitIndex & 7) == RooksFile || (bitIndex >> 3) == RooksRank)
                        score += RooksDefence;
                }
                PieceFound = true;

                RooksFile = bitIndex & 7;
                RooksRank = bitIndex >> 3;

                if (RooksRank == 6)
                    score += Rook7Rank;

                score += RookValue; // No Rook Table
            }

            #endregion

            #region // White Queen

            value = game.Position.BoardPieces[5].Value;
            while (value != 0)
            {
                bitIndex = LeastSignificantBit(value);
                value &= value - 1;// ^= 1UL << bitIndex; // &= ~(1UL << bitIndex);

                score += QueenValue; // No Queen Table
            }

            #endregion


            #region // Black Knight

            PieceFound = false;
            value = game.Position.BoardPieces[10].Value;
            while (value != 0)
            {
                bitIndex = LeastSignificantBit(value);
                value &= value - 1;// ^= 1UL << bitIndex; // &= ~(1UL << bitIndex);
                
                if (PieceFound)
                    score -= KnightPair;
                PieceFound = true;

                score -= KnightValue + KnightTable[63 - bitIndex];
            }

            #endregion

            #region // Black Bishop

            PieceFound = false;
            value = game.Position.BoardPieces[11].Value;
            while (value != 0)
            {
                bitIndex = LeastSignificantBit(value);
                value &= value - 1;// ^= 1UL << bitIndex; // &= ~(1UL << bitIndex);
                
                if (PieceFound)
                    score -= BishopPair;
                PieceFound = true;

                score -= BishopValue + BishopTable[63 - bitIndex];
            }

            #endregion

            #region // Black Rook

            PieceFound = false;
            value = game.Position.BoardPieces[12].Value;
            while (value != 0)
            {
                bitIndex = LeastSignificantBit(value);
                value &= value - 1;// ^= 1UL << bitIndex; // &= ~(1UL << bitIndex);

                if (PieceFound)
                {
                    score -= RookPair;
                    if ((bitIndex & 7) == RooksFile || (bitIndex >> 3) == RooksRank)
                        score -= RooksDefence;
                }
                PieceFound = true;

                RooksFile = bitIndex & 7;
                RooksRank = bitIndex >> 3;

                if (RooksRank == 1)
                    score -= Rook7Rank;

                score -= RookValue;
            }

            #endregion

            #region // Black Queen

            value = game.Position.BoardPieces[13].Value;
            while (value != 0)
            {
                bitIndex = LeastSignificantBit(value);
                value &= value - 1;// ^= 1UL << bitIndex; // &= ~(1UL << bitIndex);

                score -= QueenValue;
            }

            #endregion

            evaluations++;
            return game.State.SideToMove == 0 ? score : -score;
        }


        int[] PieceValues = new[] { 90, 320, 340, 500, 975, -90, -320, -340, -500, -975 };

        int[] PiecesIndexes = new[] { 1, 2, 3, 4, 5, 9, 10, 11, 12, 13 };

        int evaluate(IGame game)
        {
            int score = 0;

            var bitIndex = 0;
            for (int boardIdx = 0; boardIdx < 10; boardIdx++)
            {
                ulong value = game.Position.BoardPieces[PiecesIndexes[boardIdx]].Value;

                while (value != 0)
                {
                    bitIndex = LeastSignificantBit(value); //MostSignificantBit(value);

                    score += PieceValues[boardIdx];
                    value &= value - 1;// ^= 1UL << bitIndex; // &= ~(1UL << bitIndex);
                }
            }

            evaluations++;
            return score;
        }

        int evaluateReletive(IGame game)
        {
            int score = 0;

            var bitIndex = 0;
            for (int boardIdx = 0; boardIdx < 10; boardIdx++)
            {
                ulong value = game.Position.BoardPieces[PiecesIndexes[boardIdx]].Value;

                while (value != 0)
                {
                    bitIndex = LeastSignificantBit(value); //MostSignificantBit(value);

                    score += PieceValues[boardIdx];
                    value &= value - 1;// ^= 1UL << bitIndex; // &= ~(1UL << bitIndex);
                }
            }

            evaluations++;
            return game.State.SideToMove == 0 ? score : -score;
        }

        #endregion

        #region Bit Manipulation | Bit Masks

        byte[] index64 = {
                0, 47,  1, 56, 48, 27,  2, 60,
               57, 49, 41, 37, 28, 16,  3, 61,
               54, 58, 35, 52, 50, 42, 21, 44,
               38, 32, 29, 23, 17, 11,  4, 62,
               46, 55, 26, 59, 40, 36, 15, 53,
               34, 51, 20, 43, 31, 22, 10, 45,
               25, 39, 14, 33, 19, 30,  9, 24,
               13, 18,  8, 12,  7,  6,  5, 63
            };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        byte LeastSignificantBit(ulong bb)
        {
            /*uint fold;
            b ^= (b - 1);
            fold = ((uint)b) ^ ((uint)(b >> 32));
            return LsbDeBruijnMap[(fold * 0x78291acf) >> 26];
            */

            const ulong debruijn64 = 0x03f79d71b4cb0a89;
            return index64[((bb ^ (bb - 1)) * debruijn64) >> 58];
        }

        #endregion

        #region Tools 

        #region Zobrist
        ulong[,] zobristMatrix = new ulong[15, 64];

        ulong[] EnPassant = new ulong[66];

        ulong[] sideToMove = new ulong[2];

        ulong[] castling = new ulong[64];

        void IniZobristMatrix(ulong seed) {
            //RKiss rnd = new RKiss(seed);
            var rnd = new Random((int)seed);
            for (int i = 0; i < 15; i++) {
                for (int z = 0; z < 64; z++)
                {
                    zobristMatrix[i, z] = Get64BitRandom();//rnd.Rand();
                }
            }

            for (int z = 0; z < 66; z++)
            {
                EnPassant[z] = Get64BitRandom();//rnd.Rand();
            }

            for (int z = 0; z < 64; z++)
            {
                castling[z] = Get64BitRandom();//rnd.Rand();
            }

            for (int z = 0; z < 2; z++)
            {
                sideToMove[z] = Get64BitRandom();//rnd.Rand();
            }

            ulong Get64BitRandom()
            {
                // Get a random array of 8 bytes. 
                // As an option, you could also use the cryptography namespace stuff to generate a random byte[8]
                byte[] buffer = new byte[sizeof(ulong)];
                rnd.NextBytes(buffer);
                return BitConverter.ToUInt64(buffer, 0);// % (maxValue - minValue + 1) + minValue;
            }

        }

        ulong getKey() {
            ulong key = sideToMove[board.State.SideToMove.Side];

            //key ^= EnPassant[board.State.EnPassantSquare.AsInt()];

            //key ^= sideToMove[board.State.SideToMove.Side];

            //key ^= castling[board.State.CastlelingRights.AsInt()];

            for (int i = 1; i < 7; i++)
            {
                ulong value = board.Position.BoardPieces[i].Value;
                while (value != 0) {
                    var bitIndex = LeastSignificantBit(value);
                    key ^= zobristMatrix[i, bitIndex];
                    value &= ~(1UL << bitIndex);
                }
            }

            for (int i = 9; i < 15; i++)
            {
                ulong value = board.Position.BoardPieces[i].Value;
                while (value != 0)
                {
                    var bitIndex = LeastSignificantBit(value);
                    key ^= zobristMatrix[i, bitIndex];
                    value &= ~(1UL << bitIndex);
                }
            }

            return key;
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MoveList genMoves(bool NullSearch = false)
        {
            fullGenCalls++;
            return board.Position.GenerateMoves();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        MoveList genQuietMoves()
        {
            fullGenCalls++;
            return board.Position.GenerateQuietMoves_Mod(); //GenerateMoves(Emgf.Quiet);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        MoveList genCaptureMoves()
        {
            fullGenCalls++;
            return board.Position.GenerateCaprutePromotionsMoves_Mod(); //GenerateMoves(Emgf.Quiet);
        }

        public string[,] GetAllMoves()
        {
            var moveList = genMoves();
            var legalMoves = new string[moveList.Count, 2];

            for (int Idx = 0; Idx < moveList.Count; Idx++)
            {
                board.MakeMove(moveList[Idx]);
                legalMoves[Idx, 0] = board.GetFen().ToString();
                legalMoves[Idx, 1] = moveList[Idx].ToString();
                board.TakeMove();
            }

            return legalMoves;
        }

        void FillOpenFile() {
            /*
             * 
             * // King Sagety
             * for (int i = 0; i < 64 - 16; i++)
            {
                var j = i + 8;
                temp[0, i] |= (Files[(Math.Max(0, j % 8 - 1))] | Files[(j % 8)] | Files[(Math.Min(7, j % 8 + 1))]) 
                    & (Ranks[(j / 8)] | Ranks[(Math.Min(7, j / 8 + 1))]);
            }

            for (int i = 16; i < 64; i++)
            {
                var j = i - 8;
                temp[1, i] |= (Files[(Math.Max(0, j % 8 - 1))] | Files[(j % 8)] | Files[(Math.Min(7, j % 8 + 1))])
                    & (Ranks[(j / 8)] | Ranks[(Math.Max(0, j / 8 - 1))]);
            }
             * 
             * 
           for (int i = 0; i < 64; i++)
           {
               for (int j = i / 8 + 1; j < 8; j++)
               {
                   PawnOpenFile[0][i] |= 1UL << (i % 8 + j * 8);
               }
           }

           for (int i = 63; i >= 0; i--)
           {
               for (int j = (i / 8) - 1; j >= 0; j--)
               {
                   PawnOpenFile[1][i] |= 1UL << (i % 8 + j * 8);
               }
           }

           for (int i = 0; i < 64; i++)
           {
               int Idx;
               if (i % 8 != 0) {
                   Idx = i - 1;
                   for (int j = Idx / 8 + 1; j < 8; j++)
                   {
                       temp[0, i] |= 1UL << (Idx % 8 + j * 8);
                   }
               }

               Idx = i;
               for (int j = Idx / 8 + 1; j < 8; j++)
               {
                   temp[0, i] |= 1UL << (Idx % 8 + j * 8);
               }

               if (i % 8 != 7)
               {
                   Idx = i + 1;
                   for (int j = Idx / 8 + 1; j < 8; j++)
                   {
                       temp[0, i] |= 1UL << (Idx % 8 + j * 8);
                   }
               }
           }

           for (int i = 63; i >= 0; i--)
           {
               int Idx;
               if (i % 8 != 0)
               {
                   Idx = i - 1;
                   for (int j = (Idx / 8) - 1; j >= 0; j--)
                   {
                       temp[1, i] |= 1UL << (Idx % 8 + j * 8);
                   }
               }

               Idx = i;
               for (int j = (Idx / 8) - 1; j >= 0; j--)
               {
                   temp[1, i] |= 1UL << (Idx % 8 + j * 8);
               }

               if (i % 8 != 7)
               {
                   Idx = i + 1;
                   for (int j = (Idx / 8) - 1; j >= 0; j--)
                   {
                       temp[1, i] |= 1UL << (Idx % 8 + j * 8);
                   }
               }
           }

           */
        }

        #endregion
    }

    struct TTStructure {
        public Node reachedNode;
        public int reachedDepth;
        public short PVmoveId;
    }

    struct Node
    {
        public int score;
        public NodeType type;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Node operator -(Node a) => new Node { score = -a.score, type = a.type};
    }

    enum NodeType : byte
    {
        ExactNode,
        CutNode,
        TTExactNode,
        TTCutNode,
        Valid,
        LBound,
        UBound,
        StaticNull,
        NullMove
    }

    enum ThreadStatus : byte
    {
        Pending,
        Evaluating,
        Finished
    }
}
