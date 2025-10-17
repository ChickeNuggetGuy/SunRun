using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : Manager<GameManager>
{
	
	[SerializeField] GameObject player;
	public override string ManagerName { get => "GameManager"; }


	private void Start()
	{
		player.GetComponent<PlayerHealth>().StatReachedMin += OnhealthReachedMin;
	}

	private void OnhealthReachedMin(object sender, EventArgs e)
	{
		SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex );
	}
	
}
