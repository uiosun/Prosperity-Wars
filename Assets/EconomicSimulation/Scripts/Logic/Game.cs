﻿using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using Nashet.UnityUIUtils;
using Nashet.MarchingSquares;
using Nashet.ValueSpace;
using Nashet.Utils;

namespace Nashet.EconomicSimulation
{
    public class Game : ThreadedJob
    {
        static private readonly bool readMapFormFile = false;
        static private MyTexture map;
        static public GameObject mapObject;
        static internal GameObject r3dTextPrefab;

        static public Country Player;

        static bool haveToRunSimulation;
        static bool haveToStepSimulation;

        static public System.Random Random = new System.Random();

        static public Province selectedProvince;
        static public Province previoslySelectedProvince;

        static internal List<BattleResult> allBattles = new List<BattleResult>();

        static public readonly Market market;


        //static public MyDate date = new MyDate(0);
        static internal bool devMode = true;
        static private int mapMode;
        static private bool surrended = devMode;
        static internal Material defaultCountryBorderMaterial, defaultProvinceBorderMaterial, selectedProvinceBorderMaterial,
            impassableBorder;

        static private List<Province> seaProvinces;
        static private VoxelGrid grid;

        private readonly Rect mapBorders;
        static Game() 
        {
            Product.init(); // to avoid crash based on initialization order
            market = new Market();
        }
        public Game()
        {  
            if (readMapFormFile)
            {
                Texture2D mapImage = Resources.Load("provinces", typeof(Texture2D)) as Texture2D; ///texture;                
                map = new MyTexture(mapImage);
            }
            else
                generateMapImage();
            mapBorders = new Rect(0f, 0f, map.getWidth() * Options.cellMultiplier, map.getHeight() * Options.cellMultiplier);
        }
        public void initialize()
        {
            market.initialize();

            //FactoryType.getResourceTypes(); // FORCING FactoryType to initializate?

            updateStatus("Reading provinces..");
            Province.preReadProvinces(Game.map, this);
            seaProvinces = getSeaProvinces();
            deleteSomeProvinces();

            updateStatus("Making grid..");
            grid = new VoxelGrid(map.getWidth(), map.getHeight(), Options.cellMultiplier * map.getWidth(), map, Game.seaProvinces, this, Province.allProvinces);

            updateStatus("Making countries..");
            Country.makeCountries(this);

            updateStatus("Making population..");
            сreateRandomPopulation();

            setStartResources();
            if (!devMode)
                makeHelloMessage();
            updateStatus("Finishing generation..");
        }
        public static void setUnityAPI()
        {
            // Assigns a material named "Assets/Resources/..." to the object.
            //defaultCountryBorderMaterial = Resources.Load("materials/CountryBorder", typeof(Material)) as Material;
            defaultCountryBorderMaterial = GameObject.Find("CountryBorderMaterial").GetComponent<MeshRenderer>().material;

            //defaultProvinceBorderMaterial = Resources.Load("materials/ProvinceBorder", typeof(Material)) as Material;
            defaultProvinceBorderMaterial = GameObject.Find("ProvinceBorderMaterial").GetComponent<MeshRenderer>().material;

            //selectedProvinceBorderMaterial = Resources.Load("materials/SelectedProvinceBorder", typeof(Material)) as Material;
            selectedProvinceBorderMaterial = GameObject.Find("SelectedProvinceBorderMaterial").GetComponent<MeshRenderer>().material;

            //impassableBorder = Resources.Load("materials/ImpassableBorder", typeof(Material)) as Material;
            impassableBorder = GameObject.Find("ImpassableBorderMaterial").GetComponent<MeshRenderer>().material;

            //r3dTextPrefab = (GameObject)Resources.Load("prefabs/3dProvinceNameText", typeof(GameObject));
            r3dTextPrefab = GameObject.Find("3dProvinceNameText");

            mapObject = GameObject.Find("MapObject");
            Province.generateUnityData(grid);
            Country.setUnityAPI();
            seaProvinces = null;
            grid = null;
            map = null;
            // Annex all countries to P)layer
            //foreach (var item in Country.allCountries)
            //{
            //    item.annexTo(Game.Player);
            //}
        }
        public Rect getMapBorders()
        {
            return mapBorders;
        }
        static List<Province> getSeaProvinces()
        {
            List<Province> res = new List<Province>();
            if (!readMapFormFile)
            {
                Province seaProvince;
                for (int x = 0; x < map.getWidth(); x++)
                {
                    seaProvince = Province.find(map.GetPixel(x, 0));
                    if (!res.Contains(seaProvince))
                        res.Add(seaProvince);
                    seaProvince = Province.find(map.GetPixel(x, map.getHeight() - 1));
                    if (!res.Contains(seaProvince))
                        res.Add(seaProvince);
                }
                for (int y = 0; y < map.getHeight(); y++)
                {
                    seaProvince = Province.find(map.GetPixel(0, y));
                    if (!res.Contains(seaProvince))
                        res.Add(seaProvince);
                    seaProvince = Province.find(map.GetPixel(map.getWidth() - 1, y));
                    if (!res.Contains(seaProvince))
                        res.Add(seaProvince);
                }

                seaProvince = Province.find(map.getRandomPixel());
                if (!res.Contains(seaProvince))
                    res.Add(seaProvince);

                if (Game.Random.Next(3) == 1)
                {
                    seaProvince = Province.find(map.getRandomPixel());
                    if (!res.Contains(seaProvince))
                        res.Add(seaProvince);
                    if (Game.Random.Next(20) == 1)
                    {
                        seaProvince = Province.find(map.getRandomPixel());
                        if (!res.Contains(seaProvince))
                            res.Add(seaProvince);
                    }
                }
            }
            else
            {
                foreach (var item in Province.allProvinces)
                {
                    var color = item.getColorID();
                    if (color.g + color.b >= 200f / 255f + 200f / 255f && color.r < 96f / 255f)
                        //if (color.g + color.b + color.r > 492f / 255f)
                        res.Add(item);

                }
            }
            return res;
        }
        internal static void takePlayerControlOfThatCountry(Country country)
        {
            //if (country != Country.NullCountry)
            {
                surrended = false;
                Player = country;
                MainCamera.politicsPanel.selectReform(null);
                MainCamera.inventionsPanel.selectInvention(null);

                // not necessary since it will change automatically on province selection
                MainCamera.buildPanel.selectFactoryType(null);

                MainCamera.refreshAllActive();
            }
        }

        public static void givePlayerControlToAI()
        {
            surrended = true;
        }
        static private void deleteSomeProvinces()
        {
            //Province.allProvinces.FindAndDo(x => blockedProvinces.Contains(x.getColorID()), x => x.removeProvince());
            foreach (var item in Province.allProvinces.ToArray())
                if (seaProvinces.Contains(item))
                {
                    Province.allProvinces.Remove(item);
                    //item.removeProvince();
                }
            //todo move it in seaProvinces
            if (!readMapFormFile)
            {
                int howMuchLakes = Province.allProvinces.Count / Options.ProvinceLakeShance + Game.Random.Next(3);
                for (int i = 0; i < howMuchLakes; i++)
                    Province.allProvinces.Remove(Province.allProvinces.Random());
            }
        }

        static private void setStartResources()
        {
            //Country.allCountries[0] is null country
            //Country.allCountries[1].Capital.setResource(Product.Wood);// player

            //Country.allCountries[0].Capital.setResource(Product.Wood;
            Country.allCountries[2].Capital.setResource(Product.Fruit);
            Country.allCountries[3].Capital.setResource(Product.Gold);
            Country.allCountries[4].Capital.setResource(Product.Cotton);
            Country.allCountries[5].Capital.setResource(Product.Stone);
            Country.allCountries[6].Capital.setResource(Product.MetalOre);
            Country.allCountries[7].Capital.setResource(Product.Wood);
        }

        internal static int getMapMode()
        {
            return mapMode;
        }

        public static void redrawMapAccordingToMapMode(int newMapMode)
        {
            mapMode = newMapMode;
            foreach (var item in Province.allProvinces)
                item.updateColor(item.getColorAccordingToMapMode());
        }

        internal static void continueSimulation()
        {
            haveToRunSimulation = true;
        }

        internal static bool isRunningSimulation()
        {
            return (haveToRunSimulation || haveToStepSimulation);// && !MessagePanel.IsOpenAny();
        }
        internal static void pauseSimulation()
        {
            haveToRunSimulation = false;
        }
        internal static void makeOneStepSimulation()
        {
            haveToStepSimulation = true;
        }


        static void сreateRandomPopulation()
        {

            foreach (Province province in Province.allProvinces)
            {
                if (province.Country == Country.NullCountry)
                {
                    Tribesmen f = new Tribesmen(PopUnit.getRandomPopulationAmount(500, 1000), province.Country.getCulture(), province);
                }
                else
                {
                    PopUnit pop;
                    //if (Game.devMode)
                    //    pop = new Tribesmen(2000, province.Country.getCulture(), province);
                    //else
                    pop = new Tribesmen(PopUnit.getRandomPopulationAmount(3600, 4000), province.Country.getCulture(), province);
                    
                    //if (Game.devMode)
                    //    pop = new Aristocrats(1000, province.Country.getCulture(), province);
                    //else
                    pop = new Aristocrats(PopUnit.getRandomPopulationAmount(800, 1000), province.Country.getCulture(), province);
                                       
                    pop.GiveMoneyFromNoWhere(900f);
                    pop.storage.add(new Storage(Product.Grain, 60f));
                    //if (!Game.devMode)
                    //{
                    //pop = new Capitalists(PopUnit.getRandomPopulationAmount(500, 800), Country.getCulture(), province);
                    //pop.Cash.set(9000);

                    pop = new Artisans(PopUnit.getRandomPopulationAmount(500, 800), province.Country.getCulture(), province);
                    pop.GiveMoneyFromNoWhere(900f);

                    pop = new Farmers(PopUnit.getRandomPopulationAmount(1000, 1100), province.Country.getCulture(), province);
                    pop.GiveMoneyFromNoWhere(20f);
                    //}
                    //province.allPopUnits.Add(new Workers(600, PopType.workers, Game.player.culture, province));              
                }
            }
        }

        internal static bool isPlayerSurrended()
        {
            return surrended;
        }

        static void generateMapImage()
        {
            int mapSize;
            int width;
            //#if UNITY_WEBGL
            if (devMode)
            {
                mapSize = 20000;
                width = 150 + Random.Next(60);
            }
            else
            {
                //mapSize = 25000;
                //width = 170 + Random.Next(65);
                mapSize = 30000;
                width = 170 + Random.Next(65);
                //mapSize = 40000;
                //width = 200 + Random.Next(80);
            }
            // 140 is sqrt of 20000
            //int width = 30 + Random.Next(12);   // 140 is sqrt of 20000
            //#else
            //        int mapSize = 40000;
            //        int width = 200 + Random.Next(80);
            //#endif          
            Texture2D mapImage = new Texture2D(width, mapSize / width);        // standard for webGL


            Color emptySpaceColor = Color.black;//.setAlphaToZero();
            mapImage.setColor(emptySpaceColor);
            int amountOfProvince;

            amountOfProvince = mapImage.width * mapImage.height / 140 + Game.Random.Next(5);
            //amountOfProvince = 400 + Game.Random.Next(100);
            for (int i = 0; i < amountOfProvince; i++)
                mapImage.SetPixel(mapImage.getRandomX(), mapImage.getRandomY(), ColorExtensions.getRandomColor());

            int emptyPixels = int.MaxValue;
            Color currentColor = mapImage.GetPixel(0, 0);
            int emergencyExit = 0;
            while (emptyPixels != 0 && emergencyExit < 100)
            {
                emergencyExit++;
                emptyPixels = 0;
                for (int j = 0; j < mapImage.height; j++) // circle by province        
                    for (int i = 0; i < mapImage.width; i++)
                    {
                        currentColor = mapImage.GetPixel(i, j);
                        if (currentColor == emptySpaceColor)
                            emptyPixels++;
                        else if (currentColor.a == 1f)
                        {
                            mapImage.drawRandomSpot(i, j, currentColor);
                        }
                    }
                mapImage.setAlphaToMax();
            }
            mapImage.Apply();
            map = new MyTexture(mapImage);
            Texture2D.Destroy(mapImage);
        }

        static bool FindProvinceCenters()
        {
            //Vector3 accu = new Vector3(0, 0, 0);
            //foreach (Province pro in Province.allProvinces)
            //{
            //    accu.Set(0, 0, 0);
            //    foreach (var c in pro.mesh.vertices)
            //        accu += c;
            //    accu = accu / pro.mesh.vertices.Length;
            //    pro.centre = accu;
            //}
            return true;

            //short[,] bordersMarkers = new short[mapImage.width, mapImage.height];

            //int foundedProvinces = 0;
            //Color currentColor;
            //short borderDeepLevel = 0;
            //short alphaChangeForLevel = 1;
            //float defaultApha = 1f;
            //int placedMarkers = 456;//random number
            ////while (Province.allProvinces.Count != foundedProvinces)

            //foreach (Province pro in Province.allProvinces)
            //{
            //    borderDeepLevel = -1;
            //    placedMarkers = int.MaxValue;
            //    int emergencyExit = 200;
            //    while (placedMarkers != 0)
            //    {
            //        emergencyExit--;
            //        if (emergencyExit == 0)
            //            break;
            //        placedMarkers = 0;
            //        borderDeepLevel += alphaChangeForLevel;
            //        for (int j = 0; j < mapImage.height; j++) // cicle by province        
            //            for (int i = 0; i < mapImage.width; i++)
            //            {

            //                currentColor = mapImage.GetPixel(i, j);
            //                //if (UtilsMy.isSameColorsWithoutAlpha(currentColor, pro.colorID) && currentColor.a == defaultApha && isThereOtherColorsIn4Negbors(i, j))
            //                // && bordersMarkers[i, j] == borderDeepLevel-1
            //                if (currentColor == pro.colorID  && isThereOtherColorsIn4Negbors(i, j, bordersMarkers, (short)(borderDeepLevel)))
            //                {
            //                    //currentColor.a = borderDeepLevel;
            //                    //mapImage.SetPixel(i, j, currentColor);
            //                    borderDeepLevel ++;
            //                    bordersMarkers[i, j] = borderDeepLevel;
            //                    borderDeepLevel--;
            //                    placedMarkers++;

            //                }
            //            }

            //        //if (placedMarkers == 0) 
            //        //    ;
            //    }
            //    //// found centers!
            //    bool wroteResult = false;
            //    //
            //    for (int j = 0; j < mapImage.height && !wroteResult; j++) // cicle by province, looking where is my centre        
            //        //&& !wroteResult
            //        for (int i = 0; i < mapImage.width && !wroteResult; i++)
            //        {
            //            currentColor = mapImage.GetPixel(i, j);
            //            //if (currentColor.a == borderDeepLevel)
            //            if (currentColor == pro.colorID && bordersMarkers[i, j] == borderDeepLevel - 1)
            //            {
            //                pro.centre = new Vector3((i + 0.5f) * Options.cellMuliplier, (j + 0.5f) * Options.cellMuliplier, 0f);
            //                wroteResult = true;
            //            }
            //        }
            //}
            //return false;
        }

        public static void prepareForNewTick()
        {
            Game.market.sentToMarket.setZero();
            foreach (Country country in Country.getAllExisting())
            {
                country.SetStatisticToZero();
                foreach (Province province in country.ownedProvinces)
                {
                    province.BalanceEmployableWorkForce();
                    {
                        foreach (var item in province.getAllAgents())
                            item.SetStatisticToZero();
                    }
                }
            }
            PopType.sortNeeds();
            Product.sortSubstitutes();
        }
        static void makeHelloMessage()
        {
            Message.NewMessage("Tutorial", "Hi, this is VERY early demo of game-like economy simulator" +
                "\n\nCurrently there is: "
                + "\n\tpopulation agents \\ factories \\ countries \\ national banks"
                + "\n\tbasic trade \\ production \\ consumption \n\tbasic warfare \n\tbasic inventions"
                + "\n\tbasic reforms (population can vote for reforms)"
                + "\n\tpopulation demotion \\ promotion to other classes \n\tmigration \\ immigration \\ assimilation"
                + "\n\tpolitical \\ culture \\ core \\ resource map mode"
                + "\n\tmovements and rebellions"
                + "\n\nYou play as " + Game.Player.FullName + " You can try to growth economy or conquer the world."
                + "\n\nOr, You can give control to AI and watch it"
                + "\n\nTry arrows or WASD for scrolling map and mouse wheel for scale"
                + "\n'Enter' key to close top window, space - to pause \\ unpause"
                + "\n\n\nI have now Patreon page where I post about that game development. Try red button below!"
                + "\nAlso I would be thankful if you will share info about this project"
                , closeText: "Ok", isDefeatingAttackersMessage: false);
        }

        private static void calcBattles()
        {
            foreach (Staff attacker in Staff.getAllStaffs().ToList())
            {
                foreach (var attackerArmy in attacker.getAttackingArmies().ToList())
                {
                    var movement = attacker as Movement;
                    if (movement == null || movement.isValidGoal()) // movements attack only if goal is still valid
                    {
                        var result = attackerArmy.attack(attackerArmy.getDestination());
                        if (result.isAttackerWon())
                        {
                            if (movement == null)
                                attackerArmy.getDestination().secedeTo(attacker as Country, true);
                            else
                                movement.onRevolutionWon();
                        }
                        else if (result.isDefenderWon())
                        {
                            if (movement != null)
                                movement.onRevolutionLost();
                        }
                        if (result.getAttacker() == Game.Player || result.getDefender() == Game.Player)
                            result.createMessage();
                    }
                    attackerArmy.sendTo(null); // go home
                }
                attacker.consolidateArmies();
            }


        }
        internal static void simulate()
        {
            if (Game.haveToStepSimulation)
                Game.haveToStepSimulation = false;

            Date.Simulate();
            // strongly before PrepareForNewTick
            Game.market.simulatePriceChangeBasingOnLastTurnData();

            // should be before PrepareForNewTick cause PrepareForNewTick hires dead workers on factories
            Game.calcBattles();

            // includes workforce balancing
            // and sets statistics to zero. Should go after price calculation
            prepareForNewTick();

            // big PRODUCE circle
            foreach (Country country in Country.getAllExisting())
                foreach (Province province in country.ownedProvinces)
                    foreach (var producer in province.getAllProducers())
                        producer.produce();

            // big CONCUME circle   
            foreach (Country country in Country.getAllExisting())
            {
                country.consumeNeeds();
                if (country.economy.getValue() == Economy.PlannedEconomy)
                {
                    //consume in PE order
                    foreach (Factory factory in country.getAllFactories())
                        factory.consumeNeeds();

                    if (country.Invented(Invention.ProfessionalArmy))
                        foreach (var item in country.getAllPopUnits(PopType.Soldiers))
                            item.consumeNeeds();

                    foreach (var item in country.getAllPopUnits(PopType.Workers))
                        item.consumeNeeds();

                    foreach (var item in country.getAllPopUnits(PopType.Farmers))
                        item.consumeNeeds();

                    foreach (var item in country.getAllPopUnits(PopType.Tribesmen))
                        item.consumeNeeds();
                }
                else  //consume in regular order
                    foreach (Province province in country.ownedProvinces)//Province.allProvinces)            
                    {
                        foreach (Factory factory in province.getAllFactories())
                        {
                            factory.consumeNeeds();
                        }
                        foreach (PopUnit pop in province.allPopUnits)
                        {
                            //That placed here to avoid issues with Aristocrats and Clerics
                            //Otherwise Aristocrats starts to consume BEFORE they get all what they should
                            if (country.serfdom.getValue() == Serfdom.Allowed || country.serfdom.getValue() == Serfdom.Brutal)
                                if (pop.shouldPayAristocratTax())
                                    pop.payTaxToAllAristocrats();
                        }
                        foreach (PopUnit pop in province.allPopUnits)
                        {
                            pop.consumeNeeds();
                        }
                    }
            }
            // big AFTER all circle
            foreach (Country country in Country.getAllExisting())
            {
                country.getMoneyForSoldProduct();
                foreach (Province province in country.ownedProvinces)//Province.allProvinces)
                {
                    foreach (Factory factory in province.getAllFactories())
                    {
                        if (country.economy.getValue() == Economy.PlannedEconomy)
                            factory.OpenFactoriesPE();
                        else
                        {
                            factory.getMoneyForSoldProduct();
                            factory.ChangeSalary();
                            factory.paySalary(); // workers get gold or food here                   
                            factory.payDividend(); // also pays taxes inside
                            factory.CloseUnprofitable();
                            factory.ownership.CalcMarketPrice();
                            Rand.Call(() =>
                            {
                                factory.ownership.SellLowMarginShares();
                            }, 20);
                        }
                    }
                    province.DestroyAllMarkedfactories();
                    // get pop's income section:
                    foreach (PopUnit pop in province.allPopUnits)
                    {
                        if (pop.Type == PopType.Workers)
                            pop.LearnByWork();
                        if (pop.canSellProducts())
                            pop.getMoneyForSoldProduct();
                        pop.takeUnemploymentSubsidies();
                        if (country.Invented(Invention.ProfessionalArmy) && country.economy.getValue() != Economy.PlannedEconomy)
                        // don't need salary with PE
                        {
                            var soldier = pop as Soldiers;
                            if (soldier != null)
                                soldier.takePayCheck();
                        }
                        //because income come only after consuming, and only after FULL consumption
                        //if (pop.canTrade() && pop.hasToPayGovernmentTaxes())
                        // POps who can't trade will pay tax BEFORE consumption, not after
                        // Otherwise pops who can't trade avoid tax
                        // pop.Country.TakeIncomeTax(pop, pop.moneyIncomethisTurn, pop.Type.isPoorStrata());//pop.payTaxes();
                        pop.calcLoyalty();
                        //if (Game.Random.Next(10) == 1)
                        {
                            pop.calcGrowth();
                            pop.calcPromotions();
                            if (pop.needsFulfilled.isSmallerOrEqual(Options.PopNeedsEscapingLimit))
                                pop.EscapeForBetterLife(x => x.HasJobsFor(pop.Type, province));
                            pop.calcAssimilations();
                        }
                        if (country.economy.getValue() != Economy.PlannedEconomy)
                            Rand.Call(() => pop.invest(), Options.PopInvestRate);
                    }
                    if (country.isAI())
                        country.invest(province);
                    //if (Game.random.Next(3) == 0)
                    //    province.consolidatePops();                
                    foreach (PopUnit pop in PopUnit.PopListToAddToGeneralList)
                    {
                        PopUnit targetToMerge = pop.Province.getSimilarPopUnit(pop);
                        if (targetToMerge == null)
                            pop.Province.allPopUnits.Add(pop);
                        else
                            targetToMerge.mergeIn(pop);
                    }
                    province.allPopUnits.RemoveAll(x => !x.isAlive());
                    PopUnit.PopListToAddToGeneralList.Clear();
                    province.simulate();
                }
                country.simulate();
                if (country.isAI())
                    country.AIThink();
            }
        }

        protected override void ThreadFunction()
        {
            initialize();
        }
    }
}