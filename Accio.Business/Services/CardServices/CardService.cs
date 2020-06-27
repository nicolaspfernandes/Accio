﻿using Accio.Business.Models.CardModels;
using Accio.Business.Models.ImportModels;
using Accio.Business.Models.LanguageModels;
using Accio.Business.Models.LessonModels;
using Accio.Business.Models.RarityModels;
using Accio.Business.Models.SetModels;
using Accio.Business.Models.TypeModels;
using Accio.Business.Services.CardSearchHistoryServices;
using Accio.Business.Services.LanguageServices;
using Accio.Business.Services.LessonServices;
using Accio.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Accio.Business.Services.CardServices
{
    public class CardService
    {
        private HpTcgContext _context { get; set; }
        private SetService _cardSetService { get; set; }
        private TypeService _cardTypeService { get; set; }
        private RarityService _cardRarityService { get; set; }
        private LanguageService _languageService { get; set; }
        private LessonService _lessonService { get; set; }
        private CardSearchHistoryService _cardSearchHistoryService { get; set; }

        public CardService(HpTcgContext context, SetService cardSetService, TypeService cardTypeService,
                           RarityService cardRarityService, LanguageService languageService, LessonService lessonService,
                           CardSearchHistoryService cardSearchHistoryService)
        {
            _context = context;
            _cardSetService = cardSetService;
            _cardTypeService = cardTypeService;
            _cardRarityService = cardRarityService;
            _languageService = languageService;
            _lessonService = lessonService;
            _cardSearchHistoryService = cardSearchHistoryService;
        }

        public List<CardModel> SearchCards(CardSearchParameters cardSearchParameters)
        {
            var param = cardSearchParameters;
            var utcNow = DateTime.UtcNow;

            if (param.LanguageId == null || param.LanguageId == Guid.Empty)
            {
                var englishLanguageId = _languageService.GetLanguageId(TypeOfLanguage.English);
                param.LanguageId = englishLanguageId;
            }

            var cards = (from card in _context.Card
                         join cardDetail in _context.CardDetail on card.CardId equals cardDetail.CardId
                         join language in _context.Language on cardDetail.LanguageId equals language.LanguageId
                         join cardSet in _context.Set on card.CardSetId equals cardSet.SetId
                         join cardRarity in _context.Rarity on card.CardRarityId equals cardRarity.RarityId
                         join cardType in _context.CardType on card.CardTypeId equals cardType.CardTypeId
                         join lessonType in _context.LessonType on card.LessonTypeId equals lessonType.LessonTypeId into lessonTypeDefault
                         from lessonType in lessonTypeDefault.DefaultIfEmpty()
                         where !card.Deleted && !cardSet.Deleted && !cardRarity.Deleted && !cardType.Deleted &&
                               language.LanguageId == param.LanguageId && !string.IsNullOrEmpty(cardDetail.Url)
                         select new
                         {
                             card,
                             cardDetail,
                             cardSet,
                             cardRarity,
                             cardType,
                             language,
                             lessonType
                         });

            if (param.SetId != null && param.SetId != Guid.Empty)
            {
                cards = cards.Where(x => x.card.CardSetId == param.SetId);
            }
            if (param.TypeId != null && param.TypeId != Guid.Empty)
            {
                cards = cards.Where(x => x.card.CardTypeId == param.TypeId);
            }
            if (param.RarityId != null && param.RarityId != Guid.Empty)
            {
                cards = cards.Where(x => x.card.CardRarityId == param.RarityId);
            }
            if (param.LessonCost != null && param.LessonCost >= 0)
            {
                cards = cards.Where(x => x.card.LessonCost == param.LessonCost);
            }
            if (!string.IsNullOrEmpty(param.SearchText))
            {
                cards = from card in cards
                        where EF.Functions.Like(card.cardDetail.Name, $"%{param.SearchText}%") || EF.Functions.Like(card.cardDetail.Text, $"%{param.SearchText}%")
                        select card;
            }

            var cardModels = cards.Select(x => GetCardModel(x.card, x.cardSet, x.cardRarity, x.cardType, x.cardDetail, x.language, x.lessonType)).ToList();

            _cardSearchHistoryService.PersistCardSearchHistory(param, utcNow, utcNow);

            //if (!string.IsNullOrEmpty(sortBy))
            //{
            //    cardModels = GetCardModelsSorted(cardModels, sortBy);
            //}

            return cardModels != null ? cardModels : new List<CardModel>();
        }
        public CardModel SearchSingleCard(CardSearchParameters cardSearchParameters)
        {
            var param = cardSearchParameters;
            var utcNow = DateTime.UtcNow;

            if (param.LanguageId == null || param.LanguageId == Guid.Empty)
            {
                var englishLanguageId = _languageService.GetLanguageId(TypeOfLanguage.English);
                param.LanguageId = englishLanguageId;
            }

            var cards = (from card in _context.Card
                         join cardDetail in _context.CardDetail on card.CardId equals cardDetail.CardId
                         join language in _context.Language on cardDetail.LanguageId equals language.LanguageId
                         join cardSet in _context.Set on card.CardSetId equals cardSet.SetId
                         join cardRarity in _context.Rarity on card.CardRarityId equals cardRarity.RarityId
                         join cardType in _context.CardType on card.CardTypeId equals cardType.CardTypeId
                         join lessonType in _context.LessonType on card.LessonTypeId equals lessonType.LessonTypeId into lessonTypeDefault
                         from lessonType in lessonTypeDefault.DefaultIfEmpty()
                         where !card.Deleted && !cardSet.Deleted && !cardRarity.Deleted && !cardType.Deleted &&
                               language.LanguageId == param.LanguageId && !string.IsNullOrEmpty(cardDetail.Url) &&
                               card.CardId == cardSearchParameters.CardId

                         select new
                         {
                             card,
                             cardDetail,
                             cardSet,
                             cardRarity,
                             cardType,
                             language,
                             lessonType
                         });

            var cardModel = cards.Select(x => GetCardModel(x.card, x.cardSet, x.cardRarity, x.cardType, x.cardDetail, x.language, x.lessonType)).Single();

            _cardSearchHistoryService.PersistCardSearchHistory(param, utcNow, utcNow);

            return cardModel;
        }

        public List<CardModel> GetCardModelsSorted(List<CardModel> cards, string sortBy)
        {
            switch (sortBy)
            {
                case "llth": //Lesson low to high
                    return cards.OrderBy(x => x.LessonCost).ToList();
                case "lhtl": //Lesson high to low
                    return cards.OrderByDescending(x => x.LessonCost).ToList();
                case "cnlth": //Card name low to high
                    return cards.OrderBy(x => x.Detail.Name).ToList();
                case "cnhtl": //Card name high to low
                    return cards.OrderByDescending(x => x.Detail.Name).ToList();
                default:
                    throw new Exception("Sort by option was passed into the method, but was invalid.");
            }
        }

        public List<CardModel> GetAllCards()
        {
            var cards = (from card in _context.Card
                         join cardDetail in _context.CardDetail on card.CardId equals cardDetail.CardId
                         join language in _context.Language on cardDetail.LanguageId equals language.LanguageId
                         join cardSet in _context.Set on card.CardSetId equals cardSet.SetId
                         join cardRarity in _context.Rarity on card.CardRarityId equals cardRarity.RarityId
                         join cardType in _context.CardType on card.CardTypeId equals cardType.CardTypeId
                         join lessonType in _context.LessonType on card.LessonTypeId equals lessonType.LessonTypeId into lessonTypeDefault
                         from lessonType in lessonTypeDefault.DefaultIfEmpty()
                         where !card.Deleted && !cardDetail.Deleted && !language.Deleted && !cardSet.Deleted && !cardRarity.Deleted &&
                               !cardType.Deleted
                         select new
                         {
                             card,
                             cardDetail,
                             cardSet,
                             cardRarity,
                             cardType,
                             language,
                             lessonType
                         });

            var cardModels = cards.Select(x => GetCardModel(x.card, x.cardSet, x.cardRarity, x.cardType, x.cardDetail, x.language, x.lessonType)).ToList();

            return cardModels != null ? cardModels : new List<CardModel>();
        }
        public void ImportCardsFromSets(List<ImportSetModel> sets)
        {
            var cardCache = GetAllCards();
            var setCache = _cardSetService.GetSets();
            var cardTypeCache = _cardTypeService.GetCardTypes();
            var rarityCache = _cardRarityService.GetCardRarities();
            var lessonCache = _lessonService.GetLessonTypes();

            foreach (var set in sets)
            {
                foreach (var importCard in set.Cards)
                {
                    //Between the JSON and my DB, the only way to determine if a card is a duplicate, is to compare
                    //the names between both objects
                    var cardExists = cardCache.Any(x => x.Detail.Name.ToLower() == importCard.Name.ToLower());
                    if (!cardExists)
                    {
                        var card = GetCard(importCard, set.Name, setCache, cardTypeCache, rarityCache, lessonCache);
                        var cardDetail = GetCardDetail(card.CardId, importCard);

                        _context.Card.Add(card);
                        _context.CardDetail.Add(cardDetail);
                    }
                }
            }

            _context.SaveChanges();
        }

        public static CardModel GetCardModel(Card card, Set cardSet, Rarity cardRarity, CardType cardType, CardDetail cardDetail,
                                             Language language, LessonType lessonType)
        {
            return new CardModel()
            {
                CardId = card.CardId,
                CardSet = SetService.GetSetModel(cardSet),
                CardType = TypeService.GetCardTypeModel(cardType),
                Rarity = RarityService.GetRarityModel(cardRarity),
                Detail = CardDetailService.GetCardDetailModel(cardDetail, language),
                LessonType = lessonType == null ? null : LessonService.GetLessonTypeModel(lessonType),
                LessonCost = card.LessonCost,
                ActionCost = card.ActionCost,
                CardNumber = card.CardNumber,
                Orientation = card.Orientation,
                CreatedById = card.CreatedById,
                CreatedDate = card.CreatedDate,
                UpdatedById = card.UpdatedById,
                UpdatedDate = card.UpdatedDate,
                Deleted = card.Deleted,
            };
        }
        public Card GetCard(ImportCardModel importCardModel, string setName, List<SetModel> cardSetCache, List<CardTypeModel> cardTypesCache,
                            List<RarityModel> raritiesCache, List<LessonTypeModel> lessonTypeCache)
        {
            /*
             * As a general rule, each item's (set, type, raritiy), name should match up between the DB and the JSON.
             */

            var cardSet = cardSetCache.SingleOrDefault(x => x.Name.ToLower() == setName?.ToLower());
            var cardType = cardTypesCache.SingleOrDefault(x => x.Name.ToLower() == importCardModel.Type?.ToLower());
            var cardRarity = raritiesCache.SingleOrDefault(x => x.Name.ToLower() == importCardModel.Rarity?.ToLower());
            var lessonType = lessonTypeCache.SingleOrDefault(x => x.Name.ToLower() == importCardModel.LessonType?.ToLower());

            //Cards typically cost 1 action. But some cards cost more. From the official game, only adventures and characters
            //cost 2 actions. We don't want to assume the card costs 1 action because someone might screw up and forget to assign
            //a type to a card with more than 1 action. In the website, we'll hide the action cost if it's null.
            int? actionCost = null;
            if (cardType != null)
            {
                var typeOfCard = _cardTypeService.GetTypeOfCard(cardType.CardTypeId);
                if (typeOfCard == TypeOfCard.Adventure || typeOfCard == TypeOfCard.Character)
                    actionCost = 2;
                else
                    actionCost = 1;
            }

            return new Card()
            {
                CardId = Guid.NewGuid(),
                CardSetId = cardSet?.SetId,
                CardTypeId = cardType?.CardTypeId,
                CardRarityId = cardRarity?.RarityId,
                LessonTypeId = lessonType?.LessonTypeId,
                LessonCost = importCardModel.Cost == null ? null : (int?)Convert.ToInt32(importCardModel.Cost),
                ActionCost = actionCost,
                CardNumber = importCardModel.Number,
                Orientation = "Vertical", //Most cards are vertical. We'll set this to vertical by default and change it manually later
                CreatedById = Guid.Empty,
                CreatedDate = DateTime.UtcNow,
                UpdatedById = Guid.Empty,
                UpdatedDate = DateTime.UtcNow,
                Deleted = false,
            };
        }
        public CardDetail GetCardDetail(Guid cardId, ImportCardModel importCardModel)
        {
            //English is the only language for the moment
            var languageId = _languageService.GetLanguageId(TypeOfLanguage.English);

            return new CardDetail()
            {
                CardDetailId = Guid.NewGuid(),
                CardId = cardId,
                LanguageId = languageId,
                Name = importCardModel.Name,
                Text = importCardModel.Description.Text,
                Effect = importCardModel.Description.Effect,
                ToSolve = importCardModel.Description.ToSolve,
                Reward = importCardModel.Description.Reward,
                Url = null, //The URL to the card is maintained in a CDN, separate from the JSON.
                FlavorText = importCardModel.FlavorText,
                Illustrator = importCardModel.Artists != null ? string.Join(", ", importCardModel.Artists) : null,
                Copyright = null, //Copyright isn't maintained in the JSON
                CreatedById = Guid.Empty,
                CreatedDate = DateTime.UtcNow,
                UpdatedById = Guid.Empty,
                UpdatedDate = DateTime.UtcNow,
                Deleted = false,
            };
        }
    }
}