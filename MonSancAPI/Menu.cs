using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MonSancAPI
{
	public class Menu : MonoBehaviour
	{
		private void Start()
		{
			gameObject.SetActive(false);
		}

		public MenuList MenuList;

		public tk2dTiledSprite Header;

		public tk2dSlicedSprite Background;

		public BoolsMenu BoolsMenu;
	}
}
