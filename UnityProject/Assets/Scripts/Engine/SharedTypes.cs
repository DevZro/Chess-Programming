using System.Collections;
using System.Collections.Generic;

[System.Serializable]

public struct Move
{
    public ushort data; 
    public Move(int startSquare, int stopSquare, int moveFlag)
    {
        data = (ushort) ((startSquare) | (stopSquare << 6) | (moveFlag  << 12));
    }
}
