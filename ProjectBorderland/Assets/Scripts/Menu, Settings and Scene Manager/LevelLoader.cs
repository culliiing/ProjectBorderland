using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    public Animator transition;

    public float transitionTime = 1f;

    // Update is called once per frame
    void Update()
    {
        if (SceneManager.GetActiveScene().buildIndex==1)
        {
            if(Input.GetMouseButtonDown(0))
            {
                LoadNextLevel();
            }
        }
    }

    public void LoadNextLevel()
    {
        StartCoroutine(LoadLevel(SceneManager.GetActiveScene().buildIndex + 1));
    }


    IEnumerator LoadLevel(int LevelIndex)
    {
        transition.SetTrigger("Start");

        yield return new WaitForSeconds(transitionTime);

        SceneManager.LoadScene(LevelIndex);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.transform.tag == ("Player"))
        {
            LoadNextLevel();
        }

    }

}
