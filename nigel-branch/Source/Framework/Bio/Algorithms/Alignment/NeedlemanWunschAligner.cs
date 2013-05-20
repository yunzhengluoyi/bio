﻿using System;
using System.Collections.Generic;

namespace Bio.Algorithms.Alignment
{
    /// <summary>
    /// Implements the NeedlemanWunsch algorithm for global alignment.
    /// See Chapter 2 in Biological Sequence Analysis; Durbin, Eddy, Krogh and Mitchison; 
    /// Cambridge Press; 1998.
    /// </summary>
    public sealed class NeedlemanWunschAligner : PairwiseSequenceAligner
    {
        /// <summary>
        /// Gets the name of the current Alignment algorithm used.
        /// This is a overridden property from the abstract parent.
        /// This property returns the Name of our algorithm i.e 
        /// Needleman-Wunsch algorithm.
        /// </summary>
        public override string Name
        {
            get { return Properties.Resource.NEEDLEMAN_NAME; }
        }

        /// <summary>
        /// Gets the description of the NeedlemanWunsch algorithm used.
        /// This is a overridden property from the abstract parent.
        /// This property returns a simple description of what 
        /// NeedlemanWunschAligner class implements.
        /// </summary>
        public override string Description
        {
            get { return Properties.Resource.NEEDLEMAN_DESCRIPTION; }
        }

        /// <summary>
        /// This is step (2) in the dynamic programming model - to fill in the scoring matrix
        /// and calculate the traceback entries. 
        /// </summary>
        protected override IEnumerable<OptScoreMatrixCell> CreateTracebackTable(bool useAffineGapModel)
        {
            // The affine gap model (Gotoh) is unrolled into a duplicate routine since this is really
            // a massive loop.  We could combine most of the logic with delegates, but that has a severe
            // penalty in performance due to the number of times they are called.
            if (useAffineGapModel)
                return CreateAffineTracebackTable();

            int[] scoreLastRow = new int[Cols];
            int[] scoreRow = new int[Cols];
            int[][] matrix = SimilarityMatrix.Matrix;

            // Initialize the first row of the TB table
            for (int index = 1; index < Cols; index++)
                Traceback[0][index] = SourceDirection.Left;

            for (int i = 1; i < Rows; i++)
            {
                // Create the next TB row
                sbyte[] traceback = new sbyte[Cols];
                traceback[0] = SourceDirection.Up;

                if (i > 1)
                {
                    // Move current row to last row
                    Array.Copy(scoreRow, scoreLastRow, Cols);

                    // Initialize the next scoring row
                    scoreRow[0] = GapOpenCost*i;
                    for (int index = 1; index < Cols; index++)
                        scoreRow[index] = GapOpenCost*index;
                }
                else // first iteration
                {
                    scoreRow[0] = GapOpenCost;

                    // Initialize the first row
                    for (int index = 0; index < Cols; index++)
                    {
                        int initialScore = GapOpenCost * index;
                        scoreLastRow[index] = initialScore;
                        if (IncludeScoreTable)
                            ScoreTable[index] = initialScore;
                    }
                }

                for (int j = 1; j < Cols; j++)
                {
                    // Gap in reference sequence
                    int scoreAbove = scoreLastRow[j] + GapOpenCost;

                    // Gap in query sequence
                    int scoreLeft = scoreRow[j - 1] + GapOpenCost;

                    // Match/mismatch score
                    int mScore = (matrix != null) ? matrix[QuerySequence[i - 1]][ReferenceSequence[j - 1]] : SimilarityMatrix[QuerySequence[i - 1], ReferenceSequence[j - 1]];
                    int scoreDiag = scoreLastRow[j - 1] + mScore;

                    // Get the max S = MAX(diag,above,left) and assign the traceback
                    if ((scoreDiag > scoreAbove) && (scoreDiag > scoreLeft))
                    {
                        scoreRow[j] = scoreDiag;
                        traceback[j] = SourceDirection.Diagonal;
                    }
                    else if (scoreLeft > scoreAbove)
                    {
                        scoreRow[j] = scoreLeft;
                        traceback[j] = SourceDirection.Left;
                    }
                    else //if (scoreAbove > scoreLeft)
                    {
                        scoreRow[j] = scoreAbove;
                        traceback[j] = SourceDirection.Up;
                    }
                }

                // Save off the traceback step
                Traceback[i] = traceback;

                if (IncludeScoreTable)
                {
                    Array.Copy(scoreRow, 0, ScoreTable, i*Cols, Cols);
                }
            }

            // Return a single high score which is always the bottom right corner of the matrix.
            return new[]
            {
                new OptScoreMatrixCell
                {
                    Row = Rows-1, 
                    Col = Cols-1, 
                    Score = scoreRow[Cols-1],
                }
            };
        }

        /// <summary>
        /// This is step (2) in the dynamic programming model - to fill in the scoring matrix
        /// and calculate the traceback entries.  This version is used when the open/extension
        /// gap costs are different (affine gap model).
        /// </summary>
        private IEnumerable<OptScoreMatrixCell> CreateAffineTracebackTable()
        {
            // Horizontal and vertical gap counts.
            int gapStride = Cols + 1;
            int[] hgapLength = new int[(Rows + 1) * gapStride];
            int[] vgapLength = new int[(Rows + 1) * gapStride];
            int[] hgapCost = new int[(Rows + 1) * gapStride];
            int[] vgapCost = new int[(Rows + 1) * gapStride];

            int[] scoreLastRow = new int[Cols];
            int[] scoreRow = new int[Cols];
            int[][] matrix = SimilarityMatrix.Matrix;

            // Initialize the gap extension cost matrices.
            for (int i = 1; i < Rows; i++)
            {
                hgapLength[i * gapStride] = i;
                vgapLength[i * gapStride] = 1;
                hgapCost[i * gapStride] = GapExtensionCost * i;
            }
            for (int j = 1; j < Cols; j++)
            {
                hgapLength[j] = 1;
                vgapLength[j] = j;
                vgapCost[j] = GapExtensionCost * j;
            }

            // Initialize the first row of the TB table
            for (int index = 1; index < Cols; index++)
                Traceback[0][index] = SourceDirection.Left;

            for (int i = 1; i < Rows; i++)
            {
                // Create the next TB row
                sbyte[] traceback = new sbyte[Cols];
                traceback[0] = SourceDirection.Up;

                if (i > 1)
                {
                    // Move current row to last row
                    Array.Copy(scoreRow, scoreLastRow, Cols);

                    // Initialize the next scoring row
                    scoreRow[0] = GapExtensionCost * i;
                    for (int index = 1; index < Cols; index++)
                        scoreRow[index] = GapExtensionCost * index;
                }
                else // first iteration
                {
                    scoreRow[0] = GapExtensionCost;

                    // Initialize the first row
                    for (int index = 0; index < Cols; index++)
                    {
                        int initialScore = GapExtensionCost * index;
                        scoreLastRow[index] = initialScore;
                        if (IncludeScoreTable)
                            ScoreTable[index] = initialScore;
                    }
                }

                for (int j = 1; j < Cols; j++)
                {
                    // Gap in reference sequence
                    int scoreAbove;
                    int scoreAboveOpen = scoreLastRow[j] + GapOpenCost;
                    int scoreAboveExtend = vgapCost[(i-1) * gapStride + j] + GapExtensionCost;
                    if (scoreAboveOpen > scoreAboveExtend)
                    {
                        scoreAbove = scoreAboveOpen;
                    }
                    else
                    {
                        scoreAbove = scoreAboveExtend;
                        vgapLength[i * gapStride + j] = vgapLength[(i - 1) * gapStride + j] + 1;
                    }

                    // Gap in query sequence
                    int scoreLeft;
                    int scoreLeftOpen = scoreRow[j-1] + GapOpenCost;
                    int scoreLeftExtend = hgapCost[i * gapStride + (j-1)] + GapExtensionCost;
                    if (scoreLeftOpen > scoreLeftExtend)
                    {
                        scoreLeft = scoreLeftOpen;
                    }
                    else
                    {
                        scoreLeft = scoreLeftExtend;
                        hgapLength[i * gapStride + j] = hgapLength[i * gapStride + (j - 1)] + 1;
                    }

                    // Store off the gaps costs for this cell
                    hgapCost[i * gapStride + j] = scoreLeft;
                    vgapCost[i * gapStride + j] = scoreAbove;

                    // Get the exact match/mismatch score
                    int mScore = (matrix != null) ? matrix[QuerySequence[i - 1]][ReferenceSequence[j - 1]] : SimilarityMatrix[QuerySequence[i - 1], ReferenceSequence[j - 1]];
                    int scoreDiag = scoreLastRow[j - 1] + mScore;

                    // Get the max S = MAX(diag,above,left) and assign the traceback
                    if ((scoreDiag > scoreAbove) && (scoreDiag > scoreLeft))
                    {
                        scoreRow[j] = scoreDiag;
                        traceback[j] = SourceDirection.Diagonal;
                    }
                    else if (scoreLeft > scoreAbove)
                    {
                        scoreRow[j] = scoreLeft;
                        traceback[j] = SourceDirection.Left;
                    }
                    else //if (scoreAbove > scoreLeft)
                    {
                        scoreRow[j] = scoreAbove;
                        traceback[j] = SourceDirection.Up;
                    }
                }

                // Save off the TB row
                Traceback[i] = traceback;

                if (IncludeScoreTable)
                {
                    Array.Copy(scoreRow, 0, ScoreTable, i * Cols, Cols);
                }
            }

            return new[]
            {
                new OptScoreMatrixCell
                {
                    Row = Rows-1, 
                    Col = Cols-1, 
                    Score = scoreRow[Cols-1],
                }
            };
        }

        /// <summary>
        /// This method is used to determine when the traceback step is complete.
        /// </summary>
        /// <param name="row">Row</param>
        /// <param name="col">Column</param>
        /// <returns>True if we are finished with the traceback step, false if not.</returns>
        protected override bool TracebackIsComplete(int row, int col)
        {
            return Traceback[row][col] == SourceDirection.Stop;
        }
    }
}