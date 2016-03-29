﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Semantic;
using gcl2.Data;
using Attribute = Semantic.Attribute;

namespace gcl2
{    
    public class Grammar : IEnumerable<List<Production>>
    {
        private readonly Dictionary<Symbol, List<Production>> productions;
        private readonly Dictionary<Symbol, HashSet<Symbol>> calculatedFirsts;
        private readonly Dictionary<Symbol, HashSet<Symbol>> calculatedFollows;
        private readonly Dictionary<Symbol, HashSet<Production>> products;
        private readonly List<Symbol> terminals;
        private readonly List<Symbol> nonTerminals; 
        private readonly Symbol epsilon;
        private readonly Symbol endOfFile;
        private bool hasCalculatedFollows = false;
        private int symbolIdentity = 1;
        public Symbol Epsilon
        {
            get { return epsilon; }
        }

        public Symbol EndOfFile
        {
            get { return endOfFile; }
        }

        public Grammar()
        {
            productions = new Dictionary<Symbol, List<Production>>();
            epsilon = new Symbol(SymbolType.Epsilon, 0);
            endOfFile = new Symbol(SymbolType.EndOfFile, -1);
            calculatedFirsts = new Dictionary<Symbol, HashSet<Symbol>>();
            products = new Dictionary<Symbol, HashSet<Production>>();
            calculatedFollows = new Dictionary<Symbol, HashSet<Symbol>>();
            terminals = new List<Symbol>();
            nonTerminals = new List<Symbol>();
            
        }

        public Symbol NewSymbol(SymbolType type, List<Attribute> properties = null)
        {
            if (type == SymbolType.Epsilon)
                return Epsilon;
            if (type == SymbolType.EndOfFile)
                return EndOfFile;
            var symbol = new Symbol(type, symbolIdentity, properties);
            if (type == SymbolType.NonTerminal)
                nonTerminals.Add(symbol);
            else if (type == SymbolType.Terminal)
                terminals.Add(symbol);

            symbolIdentity += 1;
            return symbol;
        }

        public void Add(Symbol producer)
        {
            if (productions.ContainsKey(producer) == false)
                productions.Add(producer, new List<Production>());
        }

        public void Add(Symbol producer, Production production, params Production[] extraProductions)
        {
            Add(producer);
            productions[producer].Add(production);
            Relate(production);
            foreach (var prod in extraProductions)
            {
                productions[producer].Add(prod);
                Relate(prod);
            }
        }

        public bool Has(Symbol producer)
        {
            return productions.ContainsKey(producer);
        }

        public List<Production> Produces(Symbol symbol)
        {
            if (products.ContainsKey(symbol) == false)
                return new List<Production>();
            return new List<Production>(products[symbol]);
        }

        public HashSet<Symbol> First(Symbol symbol)
        {
            if (calculatedFirsts.ContainsKey(symbol) == true)
                return new HashSet<Symbol>(calculatedFirsts[symbol]);
            return First(new List<Symbol>() {symbol});
        }

        public HashSet<Symbol> First(IEnumerable<Symbol> symbols)
        {
            var firstSymbols = new HashSet<Symbol>();
            First(Epsilon, symbols, firstSymbols);
            return new HashSet<Symbol>(firstSymbols);
        }

        public HashSet<Symbol> Follow(Symbol symbol)
        {
            if (hasCalculatedFollows == false)
            {
                CalculateFollows();
                hasCalculatedFollows = true;
            }
            return calculatedFollows[symbol];
        }

        private void CalculateFollows()
        {
            var firstsToAdd = new List<Tuple<Symbol, List<List<Symbol>>>>();
            var followsToAdd = new List<Tuple<Symbol, Symbol>>();
            foreach (var nonTerminal in nonTerminals)
            {
                calculatedFollows[nonTerminal] = new HashSet<Symbol>();
                if (nonTerminal.Id == 1)
                    calculatedFollows[nonTerminal].Add(EndOfFile);
                foreach (var production in Produces(nonTerminal))
                {
                    for (var i = 0; i < production.Product.Count; i++)
                    {
                        if (production.Product[i] != nonTerminal) continue;
                        if (i == (production.Product.Count - 1)) //beta is empty. Nullable(beta) = true. missing emptystring support.
                        {
                            var producer = production.Producer;
                            if (producer != nonTerminal)
                            {
                                //add constraint follow(producer) is subset of Follow(N)
                                followsToAdd.Add(new Tuple<Symbol, Symbol>(nonTerminal, producer));
                            }
                        }
                        else //beta is not empty
                        {
                            var beta = new List<Symbol>();
                            for (var j = i + 1; j < production.Product.Count; j++)
                                beta.Add(production.Product[j]);
                            //add constraint firstBeta is subset of Follow(N)
                            var temp = new Tuple<Symbol, List<List<Symbol>>>(nonTerminal, new List<List<Symbol>>());
                            temp.Item2.Add(beta);
                            firstsToAdd.Add(temp);
                        }
                    }
                }
            }
            
            foreach (var tuple in firstsToAdd)
            {
                foreach (var firstBeta in tuple.Item2.Select(First))
                {
                    calculatedFollows[tuple.Item1].UnionWith(firstBeta);
                }
            }

            while (true)
            {
                var changed = false;
                foreach (var tuple in followsToAdd)
                {
                    var pastCount = calculatedFollows[tuple.Item1].Count;
                    var followProducer = calculatedFollows[tuple.Item2];
                    calculatedFollows[tuple.Item1].UnionWith(followProducer);
                    if (pastCount != calculatedFollows[tuple.Item1].Count)
                        changed = true;
                }
                if (changed == false)
                    break;
            }
        }

        //private HashSet<Symbol> Follow(Symbol symbol, ISet<Symbol> followSet, ISet<Symbol> dontRecurseSet)
        //{
        //    if (_calculatedFollows.ContainsKey(symbol) == true)
        //        return new HashSet<Symbol>(_calculatedFollows[symbol]);
        //    if(symbol.Id == 1)
        //        followSet.Add(EndOfFile);
        //    foreach (var production in Produces(symbol))
        //    {
        //        for (var i = 0; i < production.Product.Count; i++)
        //        {
        //            if (production.Product[i] == symbol)
        //            {
        //                if (i == (production.Product.Count - 1)) //beta is empty.
        //                {
        //                    if (_calculatedFollows.ContainsKey(symbol) == true)
        //                        followSet.UnionWith(_calculatedFollows[symbol]);
        //                    else if (dontRecurseSet.Contains(production.Producer) == false)
        //                    {
        //                        if(production.Product.Count > 1)
        //                           dontRecurseSet.Add(production.Producer);
        //                        followSet.UnionWith(Follow(production.Producer, new HashSet<Symbol>(), dontRecurseSet));               
        //                    }

        //                }
        //                else //beta is not empty
        //                {
        //                    var beta = new List<Symbol>();
        //                    for (var j = i + 1; j < production.Product.Count; j++)
        //                        beta.Add(production.Product[j]);
        //                    followSet.UnionWith(First(beta).Where(s => s != Epsilon));
        //                    if (First(beta).Contains(Epsilon) == true)
        //                    {
        //                        if (_calculatedFollows.ContainsKey(symbol) == true)
        //                            followSet.UnionWith(_calculatedFollows[symbol]);
        //                        else if (dontRecurseSet.Contains(production.Producer) == false)
        //                        {
        //                            dontRecurseSet.Add(production.Producer);
        //                            followSet.UnionWith(Follow(production.Producer, new HashSet<Symbol>(), dontRecurseSet));
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    if (_calculatedFollows.ContainsKey(symbol) == false)
        //        _calculatedFollows.Add(symbol, new HashSet<Symbol>(followSet));
        //    return _calculatedFollows[symbol];
        //}

        private void First(Symbol caller, IEnumerable<Symbol> symbols, HashSet<Symbol> firstSet)
        {
            foreach (var symbol in symbols.TakeWhile(symbol => symbol != caller))
            {
                if (calculatedFirsts.ContainsKey(symbol) == true)
                {
                    firstSet.UnionWith(calculatedFirsts[symbol]);
                    //firstSet.Add(symbol);
                }
                else
                {
                    if (symbol.Type == SymbolType.Terminal || symbol.Type == SymbolType.Epsilon)
                    {
                        calculatedFirsts[symbol] = new HashSet<Symbol> { symbol };
                        firstSet.Add(symbol);
                    }
                    else
                    {
                        foreach (var production in productions[symbol])
                        {
                            var newFirstSet = new HashSet<Symbol>();
                            First(symbol, production.Product, newFirstSet);
                            firstSet.UnionWith(newFirstSet);
                        }
                    }
                }

                if (calculatedFirsts.ContainsKey(symbol) == false)
                    calculatedFirsts[symbol] = new HashSet<Symbol>(firstSet);
                if (firstSet.Contains(Epsilon) == true)
                    continue;
                break;
            }
        }

        private void Relate(Production production)
        {
            foreach (var symbol in production.Product)
            {
                if (products.ContainsKey(symbol) == false)
                    products[symbol] = new HashSet<Production>();
                products[symbol].Add(production);
            }
        }

        public List<Production> this[Symbol producer]
        {
            get
            {
                return productions[producer];
            }
        }

        public IEnumerator<List<Production>> GetEnumerator()
        {
            return productions.Select(production => production.Value).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return productions.Select(production => production.Value).GetEnumerator();
        }
    }
}
