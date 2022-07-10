using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class TwistyScript : MonoBehaviour {

    public KMAudio Audio;
    public KMBombModule module;
    public KMBombInfo info;
    public List<KMSelectable> tselect;
    public Transform[] terminals;
    public Transform[] bases;
    public Transform[] plates;
    public TextMesh[] labels;
    public Renderer[] bulbs;
    public Material[] bmats;
    public Light[] lights;
    public ParticleSystem spark;

    private readonly string[] symbols = new string[8] { "\u0394", "\u0398", "\u039e", "\u03a0", "\u03a6", "\u03a7", "\u03a8", "\u03a9" };
    private readonly string[] snconnect = new string[36] 
    { "07", "16", "25", "34", "04", "15",
      "26", "37", "02", "57", "01", "13",
      "27", "00", "56", "35", "23", "44",
      "14", "47", "55", "36", "03", "06",
      "17", "33", "12", "05", "67", "11",
      "24", "66", "45", "22", "46", "77"};
    private string[] valcon = new string[6];
    private int[,][] rots = new int[4, 4][];
    private int[] lastrot = new int[2] { -1, 0 };
    private bool turn;
    private bool loose;

    private static int moduleIDCounter;
    private int moduleID;

    private int[] Connect()
    {
        int[] c = valcon.PickRandom().Select(x => x - '0').ToArray();
        if (Random.Range(0, 2) == 0)
            c = c.Reverse().ToArray();
        return c;
    }

    private void Start()
    {
        string sn = info.GetSerialNumber();
        moduleID = ++moduleIDCounter;
        for (int i = 12; i < 16; i++)
            bulbs[i].material = bmats[0];
        float mscale = module.transform.lossyScale.x;
        foreach (Light l in lights)
            l.range *= mscale;
        foreach (TextMesh t in labels)
            t.text = "";
        for (int i = 0; i < 16; i++)
        {
            rots[i / 4, i % 4] = new int[4] { -1, -1, -1, -1};
            float r = Random.Range(0, 360);
            bases[i].localEulerAngles = new Vector3(-90, r, 0);
            terminals[i].localEulerAngles = new Vector3(0, 0, -r);
        }
        for (int i = 0; i < 32; i++)
        {
            plates[i].localEulerAngles = new Vector3(0, 0, 90 * Random.Range(0, 4));
            int f = Random.Range(0, 8);
            plates[i].localScale = Vector3.Scale(plates[i].localScale, new Vector3(2 * (f / 4) - 1, 2 * ((f / 2) % 2) - 1, 2 * (f % 2) - 1));
        }
        List<int> dudselect = new List<int> { };
        for(int i = 0; i < 6; i++)
        {
            int p = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".IndexOf(sn[i]);
            valcon[i] = snconnect[p];
            dudselect.Add(valcon[i][0] - '0');
            dudselect.Add(valcon[i][1] - '0');
        }
        valcon = valcon.Distinct().ToArray();
        dudselect = dudselect.Distinct().ToList();
        for(int i = 0; i < 12; i++)
        {
            int[] c = Connect();
            rots[i / 4, i % 4][0] = c[0];
            rots[(i / 4) + 1, i % 4][2] = c[1];
            labels[i].text = symbols[c[0]];
            labels[i + 36].text = symbols[c[1]];
            c = Connect();
            rots[i % 4, i / 4][1] = c[0];
            rots[i % 4, (i / 4) + 1][3] = c[1];
            labels[((i % 4) * 4) + (i / 4) + 16].text = symbols[c[0]];
            labels[((i % 4) * 4) + (i / 4) + 49].text = symbols[c[1]];
        }
        for(int i = 0; i < 4; i++)
        {
            int r = dudselect.PickRandom();
            rots[0, i][2] = r;
            labels[i + 32].text = symbols[r];
            r = dudselect.PickRandom();
            rots[i, 3][1] = r;
            labels[(i * 4) + 19].text = symbols[r];
            r = dudselect.PickRandom();
            rots[3, i][0] = r;
            labels[i + 12].text = symbols[r];
            r = dudselect.PickRandom();
            rots[i, 0][3] = r;
            labels[(i * 4) + 48].text = symbols[r];
        }
        string[] sol = Config();
        for(int i = 0; i < 16; i++)
        {
            int r = Random.Range(0, 4);
            for (int j = 0; j < r; j++)
                StartCoroutine(Spin(i, true));
        }
        Debug.LogFormat("[Twisty Terminals #{0}] The initial configuration of the grid is:\n[Twisty Terminals #{0}] {1}", moduleID, string.Join("\n[Twisty Terminals #" + moduleID + "] ", Config()));
        Debug.LogFormat("[Twisty Terminals #{0}] The following pairs of transceivers can connect: {1}.", moduleID, string.Join(", ", valcon.Select(x => symbols[x[0] - '0'] + "\u2014" + symbols[x[1] - '0']).ToArray()));
        Debug.LogFormat("[Twisty Terminals #{0}] Solution:\n[Twisty Terminals #{0}] {1}", moduleID, string.Join("\n[Twisty Terminals #" + moduleID + "] ", sol));
        foreach (KMSelectable ter in tselect)
        {
            int t = tselect.IndexOf(ter);
            ter.OnInteract = delegate ()
            {
                if (!turn)
                    StartCoroutine(Spin(t, false));
                return false;
            };
        }
    }

    private IEnumerator Spin(int s, bool instant)
    {
        int temp = rots[s / 4, s % 4][0];
        for(int i = 0; i < 3; i++)
            rots[s / 4, s % 4][i] = rots[s / 4, s % 4][i + 1];
        rots[s / 4, s % 4][3] = temp;
        if (instant)
            terminals[s].Rotate(0, 0, 90);
        else
        {
            turn = true;
            Audio.PlaySoundAtTransform("Turn", terminals[s]);
            float e = 0;
            while (e < 0.5f)
            {
                float d = Time.deltaTime;
                e += d;
                terminals[s].Rotate(0, 0, 180 * d);
                yield return null;
            }
            terminals[s].Rotate(0, 0, 90 - (180 * e));
            for (int i = 0; i < 16; i++)
            {
                bulbs[i].material = bmats[0];
                lights[i].enabled = false;
            }
            bulbs[s].material = bmats[1];
            lights[s].color = new Color(1, 1, 1);
            lights[s].enabled = true;
            if (lastrot[0] == s)
            {
                lastrot[1]++;
                if (lastrot[1] > 3)
                {
                    spark.transform.localPosition = new Vector3(bases[s].localPosition.x, 0.06f, bases[s].localPosition.z);
                    spark.Emit(loose ? 10 : 50);
                    Audio.PlaySoundAtTransform("Spark", terminals[s]);
                    yield return new WaitForSeconds(0.5f);
                    Audio.PlaySoundAtTransform(loose ? "ShortOn" : "LongOn", transform);
                    for (int i = 0; i < 16; i++)
                        lights[i].color = new Color(1, 1, 1);
                    for (int i = loose ? 10 : 0; i < 20; i++)
                    {
                        tselect[Random.Range(0, 16)].AddInteractionPunch(0.5f);
                        for (int j = 0; j < 16; j++)
                        {
                            if (j != s)
                            {
                                if (Random.Range(0, 2) == 0)
                                {
                                    bulbs[j].material = bmats[0];
                                    lights[j].enabled = false;
                                }
                                else
                                {
                                    bulbs[j].material = bmats[1];
                                    lights[j].enabled = true;
                                }
                            }
                        }
                        yield return new WaitForSeconds((20 - i) / 20f);
                    }
                    loose = true;
                    bool[] check = new bool[16];
                    for (int i = 0; i < 16; i++)
                    {
                        if (i / 4 < 3)
                        {
                            string down = rots[i / 4, i % 4][0].ToString() + rots[(i / 4) + 1, i % 4][2].ToString();
                            if (!valcon.Contains(down) && !valcon.Contains(new string(down.Reverse().ToArray())))
                                continue;
                        }
                        if (i % 4 < 3)
                        {
                            string right = rots[i / 4, i % 4][1].ToString() + rots[i / 4, (i % 4) + 1][3].ToString();
                            if (!valcon.Contains(right) && !valcon.Contains(new string(right.Reverse().ToArray())))
                                continue;
                        }
                        if (i / 4 > 0)
                        {
                            string up = rots[i / 4, i % 4][2].ToString() + rots[(i / 4) - 1, i % 4][0].ToString();
                            if (!valcon.Contains(up) && !valcon.Contains(new string(up.Reverse().ToArray())))
                                continue;
                        }
                        if (i % 4 > 0)
                        {
                            string left = rots[i / 4, i % 4][3].ToString() + rots[i / 4, (i % 4) - 1][1].ToString();
                            if (!valcon.Contains(left) && !valcon.Contains(new string(left.Reverse().ToArray())))
                                continue;
                        }
                        check[i] = true;
                    }
                    for (int i = 0; i < 16; i++)
                    {
                        if (check[i])
                        {
                            bulbs[i].material = bmats[2];
                            lights[i].color = new Color(0, 1, 0);
                        }
                        else
                        {
                            bulbs[i].material = bmats[3];
                            lights[i].color = new Color(1, 0, 0);
                        }
                        lights[i].enabled = true;
                    }
                    Debug.LogFormat("[Twisty Terminals #{0}] Submitted the following configuration:\n[Twisty Terminals #{0}] {1}", moduleID, string.Join("\n[Twisty Terminals #" + moduleID + "] ", Config()));
                    if (check.All(x => x))
                    {
                        module.HandlePass();
                        Audio.PlaySoundAtTransform("Solve", transform);
                    }
                    else
                    {
                        module.HandleStrike();
                        Debug.LogFormat("[Twisty Terminals #{0}] The terminals {1} have invalid connections.", moduleID, string.Join(", ", Enumerable.Range(0, 16).Select(x => "ABCD"[x % 4].ToString() + "1234"[x / 4]).Where((x, i) => !check[i]).ToArray()));
                        Audio.PlaySoundAtTransform("Strike", transform);
                        lastrot[0] = -1;
                        lastrot[1] = 0;
                        turn = false;
                    }
                }
                else
                    turn = false;
            }
            else
            {
                lastrot[0] = s;
                lastrot[1] = 1;
                turn = false;
            }
        }
    }

    private string[] Config()
    {
        string[] c = new string[12];
        for(int i = 0; i < 4; i++)
        {
            for(int j = 0; j < 4; j++)
            {
                c[3 * i] += "\u25a0" + symbols[rots[i, j][2]] + "\u25a0" + " ";
                c[(3 * i) + 1] += symbols[rots[i, j][3]] + "+" + symbols[rots[i, j][1]] + " ";
                c[(3 * i) + 2] += "\u25a0" + symbols[rots[i, j][0]] + "\u25a0" + " ";
            }
        }
        return c;
    }
}
