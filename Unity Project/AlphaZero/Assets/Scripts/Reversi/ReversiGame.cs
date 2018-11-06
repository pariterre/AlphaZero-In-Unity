﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ReversiGame : Game
{
    public Piece[] pieces; // 所有棋子
    [HideInInspector] public PieceType[] boardSituation;
    private PieceType thisTurn;
    private PieceType playerPieceType { get { return playerFirst ? PieceType.Black : PieceType.White; } }

    private int width;
    ReversiPolicyValueNet policyValueNet;
    private MCTSPlayer mctsPlayer;
    private ReversiBoard board;

    public override string PlayerFirstWin { get { return width + "ReversiPlayerFirstWin"; } }
    public override string PlayerFirstLose { get { return width + "ReversiPlayerFirstLose"; } }
    public override string PlayerFirstTie { get { return width + "ReversiPlayerFirstTie"; } }
    public override string AiFirstWin { get { return width + "ReversiAIFirstWin"; } }
    public override string AiFirstLose { get { return width + "ReversiAIFirstLose"; } }
    public override string AiFirstTie { get { return width + "ReversiAIFirstTie"; } }
    public override string VersionKey { get { return width + "ReversiVersion"; } }

    protected override void Start()
    {
        width = (int)Mathf.Sqrt(pieces.Length);
        base.Start();

        boardSituation = new PieceType[pieces.Length];
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i].index = i;
        }

        policyValueNet = new ReversiPolicyValueNet(width);
        mctsPlayer = new MCTSPlayer(policyValueNet);
        board = new ReversiBoard();
    }

    public override void StartGame()
    {
        board.InitBoard();
        mctsPlayer.SetPlayerInd(ReversiBoard.players[playerFirst ? 1 : 0]);
        mctsPlayer.ResetPlayer();

        UpdateBoard();
        thisTurn = PieceType.Black;

        StartCoroutine("StartGameLater");
    }

    private IEnumerator StartGameLater()
    {
        yield return null;
        gameRunning = true;
    }

    private void UpdateBoard()
    {
        for (int i = 0; i < pieces.Length; i++)
        {
            if (board.states.ContainsKey(i))
            {
                PieceType pieceType = board.states[i] == ReversiBoard.players[0] ? PieceType.Black : PieceType.White;
                pieces[i].SetPiece(pieceType);
                boardSituation[i] = pieceType;
            }
            else
            {
                pieces[i].SetPiece(PieceType.Empty);
                boardSituation[i] = PieceType.Empty;
            }
        }
    }

    // 玩家或电脑输入
    private void Update()
    {
        if (!gameRunning) return;

        if (thisTurn == playerPieceType) // 轮到玩家
        {
            List<int> availableMoves = board.GetAvailableMoves();
            if (availableMoves[0] == width * width) // 只能过的情况
            {
                EndThisTurn(width * width);
                return;
            }

            Vector3 pointedPosition;
            if (Input.GetMouseButtonDown(0)) // 鼠标操作
            {
                pointedPosition = Input.mousePosition;
            }
            else if (Input.touchCount == 1) // 触屏操作
            {
                pointedPosition = Input.touches[0].position;
            }
            else return;

            RaycastHit2D hit2D = Physics2D.CircleCast(Camera.main.ScreenToWorldPoint(pointedPosition), 0.1f, Vector2.zero);
            if (hit2D.collider == null) return; // 没点到下棋子的位置
            Piece hitPiece = hit2D.collider.GetComponent<Piece>();
            if (boardSituation[hitPiece.index] != PieceType.Empty) return; // 点的位置已经有棋子了
            if (!availableMoves.Contains(hitPiece.index)) return; // 点的位置不合法

            // 成功下棋
            EndThisTurn(hitPiece.index);
            hintCanvas.HideHint();
        }
        else // 轮到电脑
        {
            int move = mctsPlayer.GetAction(board);
            EndThisTurn(move);
        }
    }

    private void EndThisTurn(int move)
    {
        // 判断是否有胜负
        board.DoMove(move);
        UpdateBoard();
        object[] endResult = board.GameEnd();
        bool end = (bool)endResult[0];
        int winner = (int)endResult[1];
        if (end)
        {
            EndGame(winner);
            return;
        }

        // 进入下一步
        thisTurn = (thisTurn == PieceType.Black ? PieceType.White : PieceType.Black);
    }

    private void EndGame(int winner)
    {
        string endText;
        if (winner == mctsPlayer.player)
        {
            endText = "你输了";
            if (playerFirst) PlayerPrefs.SetInt(PlayerFirstLose, PlayerPrefs.GetInt(PlayerFirstLose, 0) + 1);
            else PlayerPrefs.SetInt(AiFirstLose, PlayerPrefs.GetInt(AiFirstLose, 0) + 1);
        }
        else if (winner == -1)
        {
            endText = "平局";
            if (playerFirst) PlayerPrefs.SetInt(PlayerFirstTie, PlayerPrefs.GetInt(PlayerFirstTie, 0) + 1);
            else PlayerPrefs.SetInt(AiFirstTie, PlayerPrefs.GetInt(AiFirstTie, 0) + 1);
        }
        else
        {
            endText = "你赢了";
            if (playerFirst) PlayerPrefs.SetInt(PlayerFirstWin, PlayerPrefs.GetInt(PlayerFirstWin, 0) + 1);
            else PlayerPrefs.SetInt(AiFirstWin, PlayerPrefs.GetInt(AiFirstWin, 0) + 1);
        }

        gameRunning = false;
        gameEndText.text = endText;
        gameEndText.gameObject.SetActive(true);
        Invoke("EndGame", 2f);
    }

    public override void Hint()
    {
        if (hintCanvas.showing)
            hintCanvas.HideHint();
        else
            hintCanvas.ShowHint(thisTurn == playerPieceType ? policyValueNet.PolicyValueFn(board) : null, actionTransforms);
    }
}