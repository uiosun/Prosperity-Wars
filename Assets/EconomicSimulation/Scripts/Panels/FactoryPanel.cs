﻿using Nashet.EconomicSimulation.Reforms;
using Nashet.UnityUIUtils;
using Nashet.Utils;
using Nashet.ValueSpace;
using System;
using System.Linq;
using System.Text;
using Lean.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Nashet.EconomicSimulation
{
    public class FactoryPanel : DragPanel//for dragging
    {
        [SerializeField]
        private Button upgradeButton, reopenButton, destroyButton, buyButton, sellButton, nationalizeButton;

        [SerializeField]
        private Toggle subidize, dontHireOnSubsidies;

        [SerializeField]
        private Slider priority;

        [SerializeField]
        private Text generaltext, efficiencyText, caption, ownership, profitText;

        private Factory factory;
        private reopenButtonStatus reopenButtonflag;

        // Use this for initialization
        private void Start()
        {
            MainCamera.factoryPanel = this;
            GetComponent<RectTransform>().anchoredPosition = new Vector2(295f, -50f);
            Hide();
        }

        private enum reopenButtonStatus
        {
            reopen, close
        }

        private void setGUIElementsAccesability()
        {
            string dynText;
            upgradeButton.interactable = Factory.conditionsUpgrade.isAllTrue(Game.Player, factory, out dynText);
            upgradeButton.GetComponent<ToolTipHandler>().SetTextDynamic(() => dynText + "\n\nUpgrade makes enterprise bigger");

            subidize.interactable = Factory.conditionsSubsidize.isAllTrue(Game.Player, factory, out subidize.GetComponent<ToolTipHandler>().text);

            if (factory.IsOpen)
                reopenButtonflag = reopenButtonStatus.close;
            else
                reopenButtonflag = reopenButtonStatus.reopen;
            if (reopenButtonflag == reopenButtonStatus.close)
            {
                reopenButton.GetComponentInChildren<Text>().text = LeanLocalization.GetTranslationText("factory_panel/close");
                reopenButton.interactable = Factory.conditionsClose.isAllTrue(Game.Player, factory, out reopenButton.GetComponent<ToolTipHandler>().text);
            }
            else
            {
                reopenButton.interactable = Factory.conditionsReopen.isAllTrue(Game.Player, factory, out reopenButton.GetComponent<ToolTipHandler>().text);
                reopenButton.GetComponentInChildren<Text>().text = LeanLocalization.GetTranslationText("factory_panel/reopen");
            }

            destroyButton.interactable = Factory.conditionsDestroy.isAllTrue(Game.Player, factory, out destroyButton.GetComponent<ToolTipHandler>().text);

            nationalizeButton.interactable = Factory.conditionsNatinalize.isAllTrue(Game.Player, factory, out nationalizeButton.GetComponent<ToolTipHandler>().text);
            nationalizeButton.GetComponent<ToolTipHandler>().AddText(LeanLocalization.GetTranslationText("factory_panel/nationalize_tip"));

            priority.interactable = Factory.conditionsChangePriority.isAllTrue(Game.Player, factory, out priority.GetComponent<ToolTipHandler>().text);
            priority.GetComponent<ToolTipHandler>().text += LeanLocalization.GetTranslationText("factory_panel/priority_tip");

            subidize.interactable = Factory.conditionsSubsidize.isAllTrue(Game.Player, factory, out subidize.GetComponent<ToolTipHandler>().text);
            dontHireOnSubsidies.interactable = Factory.conditionsDontHireOnSubsidies.isAllTrue(Game.Player, factory, out dontHireOnSubsidies.GetComponent<ToolTipHandler>().text);

            priority.value = factory.getPriority();
            subidize.isOn = factory.isSubsidized();
            dontHireOnSubsidies.isOn = factory.isDontHireOnSubsidies();
        }

        public override void Refresh()
        {
            if (factory != null)
            {
                setGUIElementsAccesability();

                caption.text = factory.FullName;
                if (factory.isUpgrading())
                    caption.text += ", upgrading";
                else if (factory.isBuilding())
                    caption.text += ", building";
                else if (factory.IsClosed)
                    caption.text += ", closed";

                var sb = new StringBuilder();
                sb = new StringBuilder();

                sb.AppendFormat(LeanLocalization.GetTranslationText("factory_panel/workforce"),
                    factory.getWorkForce(), factory.AverageWorkersEducation, factory.getGainGoodsThisTurn());
                if (factory.storage.isNotZero())
                    sb.Append(LeanLocalization.GetTranslationText("pop_unit_panel/unsold")).Append(factory.storage);
                //sb.Append("\nBasic production: ").Append(factory.Type.basicProduction);
                //sb.Append("\nSent to market: ").Append(factory.getSentToMarket());
                //sb.Append("\n\nMoney income: ").Append(factory.moneyIncomeThisTurn);
                //sb.Append("\n\n").Append(factory.Register.ToString());                


                sb.Append(LeanLocalization.GetTranslationText("factory_panel/cash")).Append(factory.Cash);
                //sb.Append(", Dividends: ").Append(factory.GetDividends());
                if (factory.Type.hasInput())
                {
                    sb.Append(LeanLocalization.GetTranslationText("factory_panel/input_required"));
                    foreach (Storage next in factory.Type.resourceInput)
                        sb.Append(next.get() * factory.getLevel() * factory.GetWorkForceFulFilling().get()).Append(" ").Append(next.Product).Append(";");
                }
                else
                    sb.Append(LeanLocalization.GetTranslationText("factory_panel/input_required_no"));

                if (factory.constructionNeeds.Count() > 0)
                    sb.Append(LeanLocalization.GetTranslationText("factory_panel/upgrade")).Append(factory.constructionNeeds);

                if (factory.getDaysInConstruction() > 0)
                    sb.Append(LeanLocalization.GetTranslationText("factory_panel/construct")).Append(factory.getDaysInConstruction());
                if (factory.Type.hasInput() || factory.isBuilding() || factory.isUpgrading())
                {
                    if (factory.getInputProductsReserve().Count() > 0)
                        sb.Append(LeanLocalization.GetTranslationText("pop_unit_panel/stockpile")).Append(factory.getInputProductsReserve());
                    else
                        sb.Append(LeanLocalization.GetTranslationText("factory_panel/stockpile_no"));

                    if (factory.getConsumed().Count() > 0)
                        sb.Append(LeanLocalization.GetTranslationText("factory_panel/bought")).Append(factory.getConsumed().ToString(", "))
                            .Append(LeanLocalization.GetTranslationText("factory_panel/cost")).Append(Game.Player.market.getCost(factory.getConsumed()));
                }
                if (factory.Type.hasInput())
                    sb.Append("\n").Append(LeanLocalization.GetTranslationText("pop_unit_panel/resource_able")).Append(factory.getInputFactor());

                //if (Game.devMode)
                //    sb.Append("\nConsumed LT: ").Append(factory.getConsumedLastTurn());
                sb.Append(LeanLocalization.GetTranslationText("factory_panel/salary")).Append(factory.getSalary());
                //sb.Append(", Total: ").Append(factory.getSalaryCost());


                if (factory.getDaysUnprofitable() > 0)
                    sb.Append(LeanLocalization.GetTranslationText("factory_panel/unprofitable")).Append(factory.getDaysUnprofitable());

                if (factory.getDaysClosed() > 0)
                    sb.Append("\nDays closed: ").Append(factory.getDaysClosed());

                if (factory.loans.isNotZero())
                    sb.Append("\nLoan: ").Append(factory.loans);
                sb.AppendFormat(LeanLocalization.GetTranslationText("factory_panel/general"),
                    factory.ownership.GetAssetsValue(), factory.ownership.GetMarketValue(), factory.GetMargin(),
                    factory.ownership.GetTotalOnSale());
                //if (Game.devMode)
                //    sb.Append("\nHowMuchHiredLastTurn ").Append(shownFactory.getHowMuchHiredLastTurn());
                generaltext.text = sb.ToString();
                //+"\nExpenses:"

                var s = LeanLocalization.GetTranslationText("factory_panel/efficiency");
                efficiencyText.text = s + Factory.modifierEfficiency.getModifier(factory);
                efficiencyText.GetComponent<ToolTipHandler>().SetTextDynamic(() => s + Factory.modifierEfficiency.GetDescription(factory));

                var owners = factory.ownership.GetAllShares().OrderByDescending(x => x.Value.get()).ToList();//.getString(" ", "\n");
                ownership.text = string.Format(LeanLocalization.GetTranslationText("factory_panel/ownership"), owners[0].Key, owners[0].Value);
                ownership.GetComponent<ToolTipHandler>().SetTextDynamic(() => "Owners:\n" + owners.ToString(" ", "\n"));

                sb.Clear();
                sb.Append(LeanLocalization.GetTranslationText("factory_panel/profit"));
                if (Game.Player.economy != Economy.PlannedEconomy)
                    sb.Append(factory.getProfit().ToString("N3")).Append(LeanLocalization.GetTranslationText("other/gold_suffix"));
                else
                    sb.Append(LeanLocalization.GetTranslationText("other/unknown"));
                profitText.text = sb.ToString();
                profitText.GetComponent<ToolTipHandler>().SetTextDynamic(() => factory.Register.ToString());

                RefreshBuySellButtons();
            }
        }

        public void show(Factory fact)
        {
            factory = fact;
            Show();
        }

        public void removeFactory(Factory fact)
        {
            if (fact == factory)
            {
                factory = null;
                Hide();
            }
        }

        public void onSubsidizeValueChanged()
        {
            factory.setSubsidized(subidize.isOn);
            Refresh();
        }

        public void ondontHireOnSubsidiesValueChanged()
        {
            factory.setDontHireOnSubsidies(dontHireOnSubsidies.isOn);
        }

        public void onPriorityChanged()
        {
            factory.setPriority((int)Mathf.RoundToInt(priority.value));
        }

        public void onReopenClick()
        {
            if (reopenButtonflag == reopenButtonStatus.close)
                factory.close();
            else
                factory.open(Game.Player, true);
            Refresh();
            if (MainCamera.productionWindow.isActiveAndEnabled) MainCamera.productionWindow.Refresh();
            MainCamera.topPanel.Refresh();
            if (MainCamera.financePanel.isActiveAndEnabled) MainCamera.financePanel.Refresh();
        }

        private void RefreshBuySellButtons()
        {
            sellButton.interactable = Factory.conditionsSell.isAllTrue(Game.Player, factory, out sellButton.GetComponent<ToolTipHandler>().text);
            buyButton.interactable = Factory.conditionsBuy.isAllTrue(Game.Player, factory, out buyButton.GetComponent<ToolTipHandler>().text);

            buyButton.GetComponentInChildren<Text>().text = "Buy " + Options.PopBuyAssetsAtTime + " shares";

            var selling = factory.ownership.HowMuchSelling(Game.Player);
            if (selling.isZero())
                sellButton.GetComponentInChildren<Text>().text = "Sell shares";
            else
                sellButton.GetComponentInChildren<Text>().text = "Selling " + selling + " shares";
        }

        public void OnBuyClick()
        {
            factory.ownership.BuyStandardShare(Game.Player);
            UIEvents.RiseSomethingVisibleToPlayerChangedInWorld(EventArgs.Empty, this);
            //MainCamera.refreshAllActive();
        }

        public void OnSellClick()
        {
            factory.ownership.SetToSell(Game.Player, Options.PopBuyAssetsAtTime);
            Refresh();
        }

        public void onUpgradeClick()
        {
            //if (shownFactory.getConditionsForFactoryUpgradeFast(Game.player))
            {
                factory.upgrade(Game.Player);
                //MainCamera.refreshAllActive();
                UIEvents.RiseSomethingVisibleToPlayerChangedInWorld(EventArgs.Empty, this);
                if (Game.Player != factory.Country)
                    factory.Country.Diplomacy.ChangeRelation(Game.Player, Options.RelationImpactOnGovernmentInvestment.get());
            }
        }

        public void onDestroyClick()
        {
            //if (shownFactory.whyCantDestroyFactory() == null)
            {
                factory.destroyImmediately();
                //MainCamera.refreshAllActive();
                UIEvents.RiseSomethingVisibleToPlayerChangedInWorld(EventArgs.Empty, this);
            }
        }

        public void onNationalizeClick()
        {
            Game.Player.Nationilize(factory);
            //MainCamera.refreshAllActive();
            UIEvents.RiseSomethingVisibleToPlayerChangedInWorld(EventArgs.Empty, this);
        }
    }
}