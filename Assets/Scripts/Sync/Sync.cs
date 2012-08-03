using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public class Sync : MonoBehaviour {
	
	public static IntPtr h;
	public static IntPtr hh;
	
	public static bool NewObj = false;
	public static string NewObjName = "";
	public static string NewObjContent = "";
	
	public static bool Initialized = false;
	
	public static System.String prefix = "ccnx:/ndn/ucla.edu/apps/cqs/car/scene0";
	private static System.String topo = "ccnx:/ndn/broadcast/cqs/car/scene0";
	public static int TIMEOUT = 10;
	
	Thread oThread;
	
	public GameObject Car;
	public static string me = "";
	public static Hashtable Others;
	
	
	bool KnownCar(string name)
	{
		return Others.ContainsKey(name);
	}
	
	void Start()
	{
		// prepare
		Others = new Hashtable();
		
		// start
		h = GetHandle();
		hh = GetHandle();
		
		int res = WriteSlice(h, prefix, topo);
		print("WriteSlice returned: " + res);

		Egal.WatchOverRepo(h, prefix, topo);
		
		CarToRepo();
		
    	Egal.RegisterInterestFilter(h, me + "/state");
    
		oThread = new Thread(new ThreadStart(run));
      	oThread.Start();
	}
	
	IntPtr GetHandle()
	{
		// this is a C# expansion of Egal.GetHandle()
		IntPtr ccn = Egal.ccn_create();
		if (Egal.ccn_connect(ccn, "") == -1) 
        	print("could not connect to ccnd.\n");
		else
			print ("a handle is connected to ccnd.");
		return ccn;
	}
	
	int WriteSlice(IntPtr h, System.String p, System.String t)
	{
		// this is a C# expansion of Egal.WriteSlice
		int res;
		IntPtr prefix = Egal.ccn_charbuf_create();
		IntPtr topo = Egal.ccn_charbuf_create();
		int timeout = 10000;
		
		Egal.ccn_name_init(prefix);
    	Egal.ccn_name_init(topo);
		
		res = Egal.ccn_name_from_uri(prefix, p);
		if(res<0)
		{
			print ("Prefix not right");
			return res;
		}
		
		res = Egal.ccn_name_from_uri(topo, t);
		if(res<0)
		{
			print ("Topo not right");
			return res;
		}
		
		timeout = TIMEOUT;
    	if (timeout < -1) 
    	{
   	    	print("Timeout cannot be less than -1");
        	return -1;
    	}
    	timeout *= 1000;
		
		IntPtr slice = Egal.ccns_slice_create();
		Egal.ccns_slice_set_topo_prefix(slice, topo, prefix);
		
		res = Egal.ccns_write_slice(h, slice, prefix);
    
    	Egal.ccns_slice_destroy(ref slice);

		return res;
	}
	
	void CarToRepo()
	{
		Car = GameObject.Find ("Car");
		float pos_x = UnityEngine.Random.Range(923.3f, 993.4f);
		float pos_y = 101.1f;
		float pos_z = 1743.6f - pos_x + UnityEngine.Random.Range(0f, 20f);
		Vector3 pos = new Vector3(pos_x, pos_y, pos_z);
		Car.transform.position = pos;
		
		System.String name = Sync.prefix + "/0/" + UnityEngine.Random.Range(-999999, 999999);
		System.String content = "" + pos.x + "," + pos.y + "," + pos.z;
		print ("Writing " + name + " to repo: " + content);
			
		Egal.WriteToRepo(name, content+','+Car.GetInstanceID());
		
		Car.name = "" + Car.GetInstanceID();
		
		// Others.Add (name, content);
		me = name;
	}
	
	public void run()
	{
		Egal.ccn_run(h, -1);
	}
	
	void Update()
	{
		// read from repo for New Players	
		Egal.bufnode BufNode;
		BufNode.name = "";
		BufNode.content = "";
		BufNode.next = IntPtr.Zero;
		
		IntPtr temp = Egal.ReadFromBuffer();
		if(temp != IntPtr.Zero)
		{
			BufNode = (Egal.bufnode)Marshal.PtrToStructure(temp, typeof(Egal.bufnode));
			print(BufNode.name);
			print(BufNode.content);
			NewObjName = BufNode.name;
			NewObjContent = BufNode.content;
			Marshal.FreeCoTaskMem(temp);
			
			string shortname = "";
			int index = Sync.NewObjName.IndexOf('%');
			if(index <= 0)
				shortname = Sync.NewObjName;
			else
				shortname = Sync.NewObjName.Remove (index-1);
			
			string content = Sync.NewObjContent;
			
			if(shortname.EndsWith("/state"))
				ApplyNewState(shortname, content);
			else
				WelcomeNewPlayer(shortname, content);
		}
		
		
		// write to C state buffer
		string state = "" + Car.transform.position.x + ", " 
			+ Car.transform.position.y + ", " + Car.transform.position.z;
		Egal.WriteToStateBuffer(state, 128);
		
		// Ask for state of other players
		foreach(DictionaryEntry d in Others)
		{
			Egal.AskForState(hh, d.Key.ToString()+"/state", 1000);
		}
		Egal.ccn_run(hh, 10);
	}
	
	void ApplyNewState(string shortname, string content)
	{
		print("New State: " + shortname + ", " + content);
		int index = shortname.IndexOf("/state");
		shortname = shortname.Remove(index);
		
		print (shortname);
		print(Others[shortname]);
		string hashvalue = Others[shortname].ToString();
		string [] split = hashvalue.Split(new Char [] {','});
		string id = split[4];
		
		print(id);
		GameObject Car = GameObject.Find(id);
		
		split = content.Split(new Char [] {','});
		print(Car);
		Car.transform.position = new Vector3(Single.Parse(split[0]), Single.Parse(split[1]), Single.Parse(split[2]));
		
		NewObj = false;
	}
	
	void WelcomeNewPlayer(string shortname, string content)
	{
		if(KnownCar(shortname) == false && shortname != me)
			{
				
				print ("New Player Joined. " + shortname + ", " + content);

				string [] split = content.Split(new Char [] {','});
				Vector3 pos = new Vector3(Single.Parse(split[0]), Single.Parse(split[1]), Single.Parse(split[2]));
				GameObject NewCar;
				NewCar = Instantiate(Car, pos, Car.transform.rotation) as GameObject;
				NewCar.name = "" + NewCar.GetInstanceID();
				Others.Add(shortname, content+','+NewCar.GetInstanceID());

			}
			else
				print("Known Player. " + shortname + ", " + content);

			Sync.NewObj = false;
			Sync.NewObjName = "";
			Sync.NewObjContent = "";
	}
	
	void OnApplicationQuit() 
	{
		print ("quitting...");
		print ("killing thread...");
		Egal.ccn_set_run_timeout(h, 0);
		oThread.Abort();
		oThread.Join();
	}
}