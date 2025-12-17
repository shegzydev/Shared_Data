using UnityEngine;

public class TriangleBallPlacement : MonoBehaviour
{
    public GameObject[] balls; // Assign your array of 15 ball GameObjects in the Inspector
    public Transform startPosition;
    public float ballDiameter = 0.585f;

    void Start()
    {

    }

    [ContextMenu("PlaceBalls")]
    void PlaceBalls()
    {
        if (balls.Length != 15)
        {
            Debug.LogError("The array must contain exactly 15 balls.");
            return;
        }

        PositionBallsInTriangle();
    }

    void PositionBallsInTriangle()
    {
        int ballIndex = 0;
        int ballsInBase = 5;

        // Iterate through rows from the base to the tip
        for (int row = 0; row < ballsInBase; row++)
        {
            int ballsInThisRow = row + 1;

            // Calculate the starting z-position for this row to center it
            float rowWidth = (ballsInThisRow - 1) * ballDiameter;
            float startY = startPosition.position.y - rowWidth / 2f;

            // Iterate through balls in the current row
            for (int col = 0; col < ballsInThisRow; col++)
            {
                if (ballIndex >= balls.Length)
                {
                    break; // Safety break
                }

                // Calculate the position for the current ball
                // The X position decreases with each row to point the triangle in the -X direction
                float x = startPosition.position.x + row * ballDiameter * Mathf.Sqrt(3) / 2f;
                float z = startPosition.position.z;
                // The Z position is for the horizontal spread of balls in the row
                float y = startY + col * ballDiameter;

                balls[ballIndex].transform.position = new Vector3(x, y, z);
                ballIndex++;
            }
        }
    }
}