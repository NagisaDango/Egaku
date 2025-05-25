using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using Xenia.ColorPicker;
using Hashtable = ExitGames.Client.Photon.Hashtable;
public class PlayerAppearance : MonoBehaviour
{
    [Header("Eyes")]
    public List<Sprite> eyes;
    [SerializeField] private Image leftEye;
    [SerializeField] private Image rightEye;
    private int currentSelectedEyes;
    private int maxEyesSelected;
    private Hashtable eyeHash;
    
    [Header("Mouth")]
    public List<Sprite> mouthList;
    [SerializeField] private Image mouth;
    private int currentSelectedMouth;
    private int maxMouthSelected;
    private Hashtable mouthHash;
    
    [Header("Color")]
    [SerializeField] private Image body;
    public ColorPicker colorPicker;
    private Hashtable colorHash;
    private void Start()
    {
        colorPicker.ColorChanged.AddListener((c => body.color = c));
        maxEyesSelected = eyes.Count - 1;
        maxMouthSelected = mouthList.Count - 1;
        /*
        eyeHash = new Hashtable();
        eyeHash.Add("Eyes", currentSelectedEyes);
        PhotonNetwork.SetPlayerCustomProperties(eyeHash);
        mouthHash = new Hashtable();
        mouthHash.Add("Mouth", currentSelectedMouth);
        PhotonNetwork.SetPlayerCustomProperties(mouthHash);
        colorHash = new Hashtable();
        colorHash.Add("Color", new Vector3(body.color.r, body.color.g, body.color.b));
        PhotonNetwork.SetPlayerCustomProperties(colorHash);
        */
    }

    #region Eyes
    public void AddSelectedEyes()
    {
        if (currentSelectedEyes < maxEyesSelected)
            currentSelectedEyes++;
        else
            currentSelectedEyes = 0;
        SetEyesAppearance();
    }
    
    public void MinusSelectedEyes()
    {
        if (currentSelectedEyes > 0)
            currentSelectedEyes--;
        else
            currentSelectedEyes = maxEyesSelected;
        SetEyesAppearance();
    }
    
    private void SetEyesAppearance()
    {
        leftEye.sprite = eyes[currentSelectedEyes];
        rightEye.sprite = eyes[currentSelectedEyes];
    }
    #endregion

    #region Mouth
    public void AddSelectedMouth()
    {
        if (currentSelectedMouth < maxMouthSelected)
            currentSelectedMouth++;
        else
            currentSelectedMouth = 0;
        SetMouthAppearance();
    }
    
    public void MinusSelectedMouth()
    {
        if (currentSelectedMouth > 0)
            currentSelectedMouth--;
        else
            currentSelectedMouth = maxMouthSelected;
        SetMouthAppearance();
    }
    
    private void SetMouthAppearance()
    {
        mouth.sprite = mouthList[currentSelectedMouth];
    }
    #endregion

    public void SavePlayerProperty()
    {
        eyeHash = new Hashtable();
        eyeHash.Add("Eyes", currentSelectedEyes);
        PhotonNetwork.SetPlayerCustomProperties(eyeHash);
        
        mouthHash = new Hashtable();
        mouthHash.Add("Mouth", currentSelectedMouth);
        PhotonNetwork.SetPlayerCustomProperties(mouthHash);
        
        colorHash = new Hashtable();
        colorHash.Add("Color", new Vector3(body.color.r, body.color.g, body.color.b));
        PhotonNetwork.SetPlayerCustomProperties(colorHash);
    }
}
